using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Hosting;
using Zatca_Phase_II.Eum;
using Zatca_Phase_II.Models;
using ZATCA.EInvoice.SDK;
using ZATCA.EInvoice.SDK.Contracts;
using ZATCA.EInvoice.SDK.Contracts.Models;

namespace Zatca_Phase_II.Helpers
{
    public class ZatcaXML
    {
        private readonly IHostEnvironment _hostingEnvironment;

        private readonly bool isSimulation = false;

        public ZatcaXML(
            IHostEnvironment hostingEnvironment,
            EnvironmentTyp environment = EnvironmentTyp.Production
        )
        {
            _hostingEnvironment = hostingEnvironment;
            isSimulation = environment == EnvironmentTyp.Simulation;
        }

        public XmlDocument CreateXML(Bill x)
        {
            try
            {
                DateTime Date = DateTime.Now;
                if (DateTime.TryParse(x.Date, out var dt))
                {
                    Date = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second);
                }

                string ident =
                    "<Invoice xmlns=\"urn:oasis:names:specification:ubl:schema:xsd:Invoice-2\" xmlns:cac=\"urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2\" xmlns:cbc=\"urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2\" xmlns:ext=\"urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2\"></Invoice>";
                XDocument doc = XDocument.Parse(ident);
                XNamespace nsExt = "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2";
                XElement i = doc.Root!;
                XNamespace nsCac = i.GetNamespaceOfPrefix("cac")!;
                XNamespace nsCbc = i.GetNamespaceOfPrefix("cbc")!;

                XAttribute TextPlain = new XAttribute("mimeCode", "text/plain");
                XAttribute currencyID = new XAttribute("currencyID", "SAR");
                XAttribute schemeID05 = new XAttribute("schemeID", "UN/ECE 5305");
                XAttribute schemeID53 = new XAttribute("schemeID", "UN/ECE 5153");
                XAttribute schemeAgencyID = new XAttribute("schemeAgencyID", "6");

                XElement ProfileID = new XElement(nsCbc + "ProfileID", "reporting:1.0");
                XElement ID = new XElement(nsCbc + "ID", x.Code);
                XElement UUID = new XElement(nsCbc + "UUID", x.Uid);

                XElement IssueDate = new XElement(nsCbc + "IssueDate", Date.ToString("yyyy-MM-dd"));
                XElement IssueTime = new XElement(nsCbc + "IssueTime", Date.ToString("HH:mm:ss"));

                // ZATCA/UBL: 388=Tax Invoice, 381=Debit Note, 383=Credit Note
                var typCode = x.NoticeType switch
                {
                    NoticeType.Debit  => "381",
                    NoticeType.Credit => "383",
                    _                 => "388",
                };
                // var typCode = "388";
                var invoiceType = x.InvoiceType switch
                {
                    InvoiceType.Standard => "0100000",
                    InvoiceType.Simplified => "0200000",
                    _ => "0200000",
                };

                XElement InvoiceTypeCode = new XElement(
                    nsCbc + "InvoiceTypeCode",
                    typCode,
                    new XAttribute("name", invoiceType)
                );
                XElement Note = new XElement(
                    nsCbc + "Note",
                    x.Notes,
                    new XAttribute("languageID", "ar")
                );
                XElement DocumentCurrencyCode = new XElement(nsCbc + "DocumentCurrencyCode", "SAR");
                XElement TaxCurrencyCode = new XElement(nsCbc + "TaxCurrencyCode", "SAR");
                XElement BillingReference = new XElement(
                    nsCac + "BillingReference",
                    new XElement(
                        nsCac + "InvoiceDocumentReference",
                        new XElement(nsCbc + "ID", $"{x.ParentCode}")
                    )
                );
                XElement ICV = new XElement(
                    nsCac + "AdditionalDocumentReference",
                    new XElement(nsCbc + "ID", "ICV"),
                    new XElement(nsCbc + "UUID", x.Code)
                );

                // PIH: hash of invoice N-1. If Code is numeric use Code-1, otherwise hash "0" as seed.
                string prevCodeHash = int.TryParse(x.Code, out var codeNum) && codeNum > 1
                    ? HashHelper.ComputeSha256((codeNum - 1).ToString())
                    : HashHelper.ComputeSha256("0");
                XElement PIH = new XElement(
                    nsCac + "AdditionalDocumentReference",
                    new XElement(nsCbc + "ID", "PIH"),
                    new XElement(
                        nsCac + "Attachment",
                        new XElement(
                            nsCbc + "EmbeddedDocumentBinaryObject",
                            prevCodeHash,
                            new XAttribute(TextPlain)
                        )
                    )
                );

                XElement AccountingSupplierParty = new XElement(
                    nsCac + "AccountingSupplierParty",
                    new XElement(
                        nsCac + "Party",
                        new XElement(
                            nsCac + "PartyIdentification",
                            new XElement(
                                nsCbc + "ID",
                                x.Company.Crn,
                                new XAttribute("schemeID", "CRN")
                            )
                        ),
                        new XElement(
                            nsCac + "PostalAddress",
                            new XElement(nsCbc + "StreetName", x.Supplier.StreetName),
                            new XElement(nsCbc + "BuildingNumber", x.Supplier.BuildingNumber),
                            new XElement(
                                nsCbc + "CitySubdivisionName",
                                x.Supplier.CitySubdivisionName
                            ),
                            new XElement(nsCbc + "CityName", x.Supplier.CityName),
                            new XElement(nsCbc + "PostalZone", x.Supplier.PostalZone),
                            new XElement(
                                nsCac + "Country",
                                new XElement(nsCbc + "IdentificationCode", "SA")
                            )
                        ),
                        new XElement(
                            nsCac + "PartyTaxScheme",
                            new XElement(nsCbc + "CompanyID", x.Supplier.TaxNumber),
                            new XElement(nsCac + "TaxScheme", new XElement(nsCbc + "ID", "VAT"))
                        ),
                        new XElement(
                            nsCac + "PartyLegalEntity",
                            new XElement(nsCbc + "RegistrationName", x.Supplier?.RegistrationName)
                        )
                    )
                );

                XElement AccountingCustomerParty = new XElement(
                    nsCac + "AccountingCustomerParty",
                    new XElement(
                        nsCac + "Party",
                        new XElement(
                            nsCac + "PostalAddress",
                            new XElement(nsCbc + "StreetName", x.Customer.StreetName),
                            new XElement(nsCbc + "BuildingNumber", x.Customer.BuildingNumber),
                            new XElement(
                                nsCbc + "CitySubdivisionName",
                                x.Customer.CitySubdivisionName
                            ),
                            new XElement(nsCbc + "CityName", x.Customer.CityName),
                            new XElement(nsCbc + "PostalZone", x.Customer.PostalZone),
                            new XElement(
                                nsCac + "Country",
                                new XElement(nsCbc + "IdentificationCode", "SA")
                            )
                        ),
                        new XElement(
                            nsCac + "PartyTaxScheme",
                            new XElement(nsCbc + "CompanyID", x.Customer.TaxNumber),
                            new XElement(nsCac + "TaxScheme", new XElement(nsCbc + "ID", "VAT"))
                        ),
                        new XElement(
                            nsCac + "PartyLegalEntity",
                            new XElement(nsCbc + "RegistrationName", x.Customer.RegistrationName)
                        )
                    )
                );
                string PaymentCode = "10";
                if (x!.IsDebit == true)
                {
                    PaymentCode = "30";
                }
                XElement PaymentMeans;
                if (typCode != "388")
                {
                    // Credit/Debit notes require InstructionNote
                    PaymentMeans = new XElement(
                        nsCac + "PaymentMeans",
                        new XElement(nsCbc + "PaymentMeansCode", PaymentCode),
                        new XElement(nsCbc + "InstructionNote", "Returned items")
                    );
                }
                else
                {
                    PaymentMeans = new XElement(
                        nsCac + "PaymentMeans",
                        new XElement(nsCbc + "PaymentMeansCode", PaymentCode)
                    );
                }

                // AllowanceCharge is only required when there is an actual discount
                XElement? AllowanceCharge = x.DiscountTotal > 0
                    ? new XElement(
                        nsCac + "AllowanceCharge",
                        new XElement(nsCbc + "ChargeIndicator", "false"),
                        new XElement(nsCbc + "AllowanceChargeReason", "discount"),
                        new XElement(nsCbc + "Amount",
                            String.Format("{0:0.00}", x.DiscountTotal),
                            new XAttribute(currencyID)),
                        new XElement(
                            nsCac + "TaxCategory",
                            new XElement(
                                nsCbc + "ID", "S",
                                new XAttribute(schemeID05),
                                new XAttribute(schemeAgencyID)
                            ),
                            new XElement(nsCbc + "Percent", "15"),
                            new XElement(
                                nsCac + "TaxScheme",
                                new XElement(
                                    nsCbc + "ID", "VAT",
                                    new XAttribute(schemeID53),
                                    new XAttribute(schemeAgencyID)
                                )
                            )
                        )
                    )
                    : null;

                XElement TaxTotal = new XElement(
                    nsCac + "TaxTotal",
                    new XElement(
                        nsCbc + "TaxAmount",
                        String.Format("{0:00.00}", x.TotalVAT),
                        new XAttribute(currencyID)
                    )
                );

                XElement TaxTotalx = new XElement(
                    nsCac + "TaxTotal",
                    new XElement(
                        nsCbc + "TaxAmount",
                        String.Format("{0:00.00}", x.TotalVAT),
                        new XAttribute(currencyID)
                    ),
                    new XElement(
                        nsCac + "TaxSubtotal",
                        new XElement(
                            nsCbc + "TaxableAmount",
                            String.Format("{0:00.00}", x.TotalAfterDiscount),
                            new XAttribute(currencyID)
                        ),
                        new XElement(
                            nsCbc + "TaxAmount",
                            String.Format("{0:00.00}", x.TotalVAT),
                            new XAttribute(currencyID)
                        ),
                        new XElement(
                            nsCac + "TaxCategory",
                            new XElement(
                                nsCbc + "ID",
                                "S",
                                new XAttribute(schemeID05),
                                new XAttribute(schemeAgencyID)
                            ),
                            new XElement(nsCbc + "Percent", "15"),
                            new XElement(
                                nsCac + "TaxScheme",
                                new XElement(
                                    nsCbc + "ID",
                                    "VAT",
                                    new XAttribute(schemeID53),
                                    new XAttribute(schemeAgencyID)
                                )
                            )
                        )
                    )
                );

                double subTotal = x.VatIsTaxesIncluded ? x.SupTotal / 1.15 : x.SupTotal;

                XElement LegalMonetaryTotal = new XElement(
                    nsCac + "LegalMonetaryTotal",
                    new XElement(
                        nsCbc + "LineExtensionAmount",
                        String.Format("{0:00.00}", subTotal),
                        new XAttribute(currencyID)
                    ),
                    new XElement(
                        nsCbc + "TaxExclusiveAmount",
                        String.Format("{0:00.00}", x.TotalAfterDiscount),
                        new XAttribute(currencyID)
                    ),
                    new XElement(
                        nsCbc + "TaxInclusiveAmount",
                        String.Format("{0:00.00}", x.NetTotal),
                        new XAttribute(currencyID)
                    ),
                    new XElement(
                        nsCbc + "AllowanceTotalAmount",
                        String.Format("{0:00.00}", x.DiscountTotal),
                        new XAttribute(currencyID)
                    ),
                    new XElement(nsCbc + "PrepaidAmount", "0.00", new XAttribute(currencyID)),
                    new XElement(
                        nsCbc + "PayableAmount",
                        String.Format("{0:00.00}", x.NetTotal),
                        new XAttribute(currencyID)
                    )
                );

                // 1. UBLExtensions placeholder — required by the ZATCA SDK for digital signature injection
                XElement ublExtensions = new XElement(
                    nsExt + "UBLExtensions",
                    new XElement(
                        nsExt + "UBLExtension",
                        new XElement(nsExt + "ExtensionURI", "urn:oasis:names:specification:ubl:dsig:enveloped:xades"),
                        new XElement(nsExt + "ExtensionContent")
                    )
                );
                i.AddFirst(ublExtensions);

                i.Add(ProfileID);
                i.Add(ID);
                i.Add(UUID);
                i.Add(IssueDate);
                i.Add(IssueTime);
                i.Add(InvoiceTypeCode);
                i.Add(Note);
                i.Add(DocumentCurrencyCode);
                i.Add(TaxCurrencyCode);
                if (typCode != "388")
                    i.Add(BillingReference);
                i.Add(ICV);
                i.Add(PIH);

                // 2. QR placeholder — required by the ZATCA SDK to embed the QR code
                XElement QR = new XElement(
                    nsCac + "AdditionalDocumentReference",
                    new XElement(nsCbc + "ID", "QR"),
                    new XElement(
                        nsCac + "Attachment",
                        new XElement(
                            nsCbc + "EmbeddedDocumentBinaryObject",
                            "",
                            new XAttribute(TextPlain)
                        )
                    )
                );
                i.Add(QR);

                // 3. Signature element — required by the ZATCA SDK (UBL enveloped signature reference)
                XElement Signature = new XElement(
                    nsCac + "Signature",
                    new XElement(nsCbc + "ID", "urn:oasis:names:specification:ubl:signature:Invoice"),
                    new XElement(nsCbc + "SignatureMethod", "urn:oasis:names:specification:ubl:dsig:enveloped:xades")
                );
                i.Add(Signature);

                i.Add(AccountingSupplierParty);
                i.Add(AccountingCustomerParty);
                i.Add(PaymentMeans);
                if (AllowanceCharge != null)
                    i.Add(AllowanceCharge);
                i.Add(TaxTotal);
                i.Add(TaxTotalx);
                i.Add(LegalMonetaryTotal);

                foreach (var d in x.BillDetails.Where(d => d.IsDelete == false))
                {
                    // LineExtensionAmount = net price before VAT
                    double lineNet  = x.VatIsTaxesIncluded ? d.NetTotal / 1.15 : d.NetTotal;
                    double lineVat  = d.NetTotal - lineNet;
                    double linePrice = x.VatIsTaxesIncluded ? d.ItemSellPrice / 1.15 : d.ItemSellPrice;

                    XElement InvoiceLine = new XElement(
                        nsCac + "InvoiceLine",
                        new XElement(nsCbc + "ID", d.Id),
                        new XElement(
                            nsCbc + "InvoicedQuantity",
                            d.Qty,
                            new XAttribute("unitCode", "PCE")
                        ),
                        new XElement(
                            nsCbc + "LineExtensionAmount",
                            String.Format("{0:0.00}", lineNet),
                            new XAttribute(currencyID)
                        ),
                        new XElement(
                            nsCac + "TaxTotal",
                            new XElement(
                                nsCbc + "TaxAmount",
                                String.Format("{0:0.00}", lineVat),
                                new XAttribute(currencyID)
                            ),
                            new XElement(
                                nsCbc + "RoundingAmount",
                                String.Format("{0:0.00}", d.NetTotal),
                                new XAttribute(currencyID)
                            )
                        ),
                        new XElement(
                            nsCac + "Item",
                            new XElement(nsCbc + "Name", d.ItemName),
                            new XElement(
                                nsCac + "ClassifiedTaxCategory",
                                new XElement(nsCbc + "ID", "S"),
                                new XElement(nsCbc + "Percent", "15.00"),
                                new XElement(nsCac + "TaxScheme", new XElement(nsCbc + "ID", "VAT"))
                            )
                        ),
                        new XElement(
                            nsCac + "Price",
                            new XElement(
                                nsCbc + "PriceAmount",
                                String.Format("{0:0.00}", linePrice),
                                new XAttribute(currencyID)
                            ),
                            new XElement(
                                nsCac + "AllowanceCharge",
                                new XElement(nsCbc + "ChargeIndicator", "false"),
                                new XElement(nsCbc + "AllowanceChargeReason", "discount"),
                                new XElement(
                                    nsCbc + "Amount",
                                    String.Format("{0:0.00}", d.DiscountTotal),
                                    new XAttribute(currencyID)
                                )
                            )
                        )
                    );
                    i.Add(InvoiceLine);
                }

                // XmlDocument xmlDoc = new XmlDocument();
                // using XmlReader reader = doc.CreateReader();
                // xmlDoc.Load(reader);
                // LogsFile.MessageZatca($"CreateXML{x.Code}");
                // return GetFormattedXmlDocument(xmlDoc);

                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.PreserveWhitespace = true;
                using (XmlReader reader = doc.CreateReader())
                {
                    xmlDoc.Load(reader);
                }
                LogsFile.MessageZatca($"CreateXML{x.Code}");
                return FormatXML.PrettyXml(xmlDoc);
            }
            catch (Exception ex)
            {
                LogsFile.MessageZatca($"Error in Create XML {ex.Message}");
                // Log the exception and handle it appropriately
                throw new ApplicationException(
                    "An error occurred while creating the XML document.",
                    ex
                );
            }
        }

        /// <summary>
        /// Performs a specific XML operation:
        /// 1 = Sign, 2 = Generate QR, 3 = Generate Hash, 4 = Validate
        /// </summary>
        public Task<(dynamic?, string?)> CheckXML(int i, XmlDocument xmlDoc, ZatcaBranch obj)
        {
            if (xmlDoc == null)
                return Task.FromResult<(dynamic?, string?)>((null, "xmlDoc is null"));

            var _IEInvoiceSigningLogic = new EInvoiceSigner();
            var _IHashingValidator     = new EInvoiceHashGenerator();
            var _IQRValidator          = new EInvoiceQRGenerator();
            IEInvoiceValidator _IEInvoiceValidator = new EInvoiceValidator();

            return i switch
            {
                1 => Task.FromResult<(dynamic?, string?)>(Sign()),
                2 => Task.FromResult<(dynamic?, string?)>(QR()),
                3 => Task.FromResult<(dynamic?, string?)>(Hash()),
                4 => Task.FromResult<(dynamic?, string?)>(Validate()),
                _ => Task.FromResult<(dynamic?, string?)>((null, $"Unknown operation: {i}"))
            };

            (dynamic?, string?) Sign()
            {
                SignResult x = _IEInvoiceSigningLogic.SignDocument(xmlDoc, obj.Csr, obj.PrivateKey);
                if (x.IsValid)
                {
                    LogsFile.MessageZatca("Sign Xml OK");
                    return (
                        FormatXML.GetFormattedXmlDocument(x.SignedEInvoice),
                        x.Steps.FirstOrDefault(s => s.StepName == "Generate EInvoice Hash")?.ResultedValue
                    );
                }
                LogsFile.MessageZatca($"Error Sign Xml: {x.ErrorMessage}");
                return (null, null);
            }

            (dynamic?, string?) QR()
            {
                QRResult qr = _IQRValidator.GenerateEInvoiceQRCode(xmlDoc);
                return (qr.QR, string.Empty);
            }

            (dynamic?, string?) Hash()
            {
                var x = _IHashingValidator.GenerateEInvoiceHashing(xmlDoc);
                return (x.Hash, string.Empty);
            }

            (dynamic?, string?) Validate()
            {
                var validation = _IEInvoiceValidator.ValidateEInvoice(xmlDoc, obj.Csr, string.Empty);
                var failedSteps = validation.ValidationSteps.Where(s => !s.IsValid).ToList();
                LogsFile.MessageZatca($"Validate XML: {failedSteps.Count} failed steps");
                return (failedSteps, string.Empty);
            }
        }
    }
}
