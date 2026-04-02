using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Hosting;
using Zatca_Phase_II.Helpers;
using Zatca_Phase_II.Models;

namespace Zatca_Phase_II.Services;

public class DefaultXML()
{
    public (XmlDocument, XmlDocument, XmlDocument) CreateXmlStandarDefaultUpload(
        CompanyData obj,
        object uuids
    )
    {
        XmlDocument Simplified = CreateXmlDefault(obj, ((dynamic)uuids).UUID, "0100000");
        LogsFile.MessageZatca($"Simplified");

        XmlDocument Debit = CreateXmlDebitDefault(obj, ((dynamic)uuids).DebitUUID, "0100000");
        LogsFile.MessageZatca($"Debit");

        XmlDocument Credit = CreateXmlCreditDefault(obj, ((dynamic)uuids).CreditUUID, "0100000");
        LogsFile.MessageZatca($"Credit");

        return (Simplified, Debit, Credit);
    }
    public (XmlDocument, XmlDocument, XmlDocument) CreateXmlDefaultUpload(
        CompanyData obj,
        object uuids
    )
    {
        XmlDocument Simplified = CreateXmlDefault(obj, ((dynamic)uuids).UUID, "0200000");
        LogsFile.MessageZatca($"Simplified");

        XmlDocument Debit = CreateXmlDebitDefault(obj, ((dynamic)uuids).DebitUUID, "0200000");
        LogsFile.MessageZatca($"Debit");

        XmlDocument Credit = CreateXmlCreditDefault(obj, ((dynamic)uuids).CreditUUID, "0200000");
        LogsFile.MessageZatca($"Credit");

        return (Simplified, Debit, Credit);
    }

    public XmlDocument CreateXmlDebitDefault(CompanyData company, Guid uuid, string type)
    {
        DateTime Date = DateTime.Now;

        // Create the XML document
        XNamespace ns = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
        XNamespace cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
        XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";

        var doc = new XDocument(
            new XElement(
                ns + "Invoice",
                new XAttribute(XNamespace.Xmlns + "cac", cac),
                new XAttribute(XNamespace.Xmlns + "cbc", cbc),
                new XElement(cbc + "ProfileID", "reporting:1.0"),
                new XElement(cbc + "ID", "935"),
                new XElement(cbc + "UUID", uuid),
                new XElement(cbc + "IssueDate", Date.ToString("yyyy-MM-dd")),
                new XElement(cbc + "IssueTime", Date.ToString("HH:mm:ss")),
                new XElement(cbc + "InvoiceTypeCode", new XAttribute("name", type), "381"),
                new XElement(cbc + "DocumentCurrencyCode", "SAR"),
                new XElement(cbc + "TaxCurrencyCode", "SAR"),
                new XElement(
                    cac + "BillingReference",
                    new XElement(
                        cac + "InvoiceDocumentReference",
                        new XElement(
                            cbc + "ID",
                            $"Invoice Number: 1; Invoice Issue Date: {Date:yyyy-MM-dd}"
                        )
                    )
                ),
                new XElement(
                    cac + "AdditionalDocumentReference",
                    new XElement(cbc + "ID", "ICV"),
                    new XElement(cbc + "UUID", "70")
                ),
                new XElement(
                    cac + "AdditionalDocumentReference",
                    new XElement(cbc + "ID", "PIH"),
                    new XElement(
                        cac + "Attachment",
                        new XElement(
                            cbc + "EmbeddedDocumentBinaryObject",
                            new XAttribute("mimeCode", "text/plain"),
                            HashHelper.ComputeSha256("0")
                        )
                    )
                ),
                new XElement(
                    cac + "AdditionalDocumentReference",
                    new XElement(cbc + "ID", "QR"),
                    new XElement(
                        cac + "Attachment",
                        new XElement(
                            cbc + "EmbeddedDocumentBinaryObject",
                            new XAttribute("mimeCode", "text/plain"),
                            ""
                        )
                    )
                ),
                new XElement(
                    cac + "Signature",
                    new XElement(cbc + "ID", "urn:oasis:names:specification:ubl:signature:Invoice"),
                    new XElement(
                        cbc + "SignatureMethod",
                        "urn:oasis:names:specification:ubl:dsig:enveloped:xades"
                    )
                ),
                new XElement(
                    cac + "AccountingSupplierParty",
                    new XElement(
                        cac + "Party",
                        new XElement(
                            cac + "PartyIdentification",
                            new XElement(cbc + "ID", new XAttribute("schemeID", "CRN"), company.Crn)
                        ),
                        new XElement(
                            cac + "PostalAddress",
                            new XElement(cbc + "StreetName", "الامير سلطان | Prince Sultan"),
                            new XElement(cbc + "BuildingNumber", "2322"),
                            new XElement(cbc + "CitySubdivisionName", "المربع | Al-Murabba"),
                            new XElement(cbc + "CityName", "الرياض | Riyadh"),
                            new XElement(cbc + "PostalZone", "23333"),
                            new XElement(
                                cac + "Country",
                                new XElement(cbc + "IdentificationCode", "SA")
                            )
                        ),
                        new XElement(
                            cac + "PartyTaxScheme",
                            new XElement(cbc + "CompanyID", company.TaxNumber),
                            new XElement(cac + "TaxScheme", new XElement(cbc + "ID", "VAT"))
                        ),
                        new XElement(
                            cac + "PartyLegalEntity",
                            new XElement(cbc + "RegistrationName", company.Name)
                        )
                    )
                ),
                CreateAccountingCustomerParty(type, cac, cbc),
                // new XElement(cac + "AccountingCustomerParty",
                //     new XElement(cac + "Party",
                //         new XElement(cac + "PostalAddress",
                //             new XElement(cbc + "StreetName", "صلاح الدين | Salah Al-Din"),
                //             new XElement(cbc + "BuildingNumber", "1111"),
                //             new XElement(cbc + "CitySubdivisionName", "المروج | Al-Murooj"),
                //             new XElement(cbc + "CityName", "الرياض | Riyadh"),
                //             new XElement(cbc + "PostalZone", "12222"),
                //             new XElement(cac + "Country",
                //                 new XElement(cbc + "IdentificationCode", "SA")
                //             )
                //         ),
                //         new XElement(cac + "PartyTaxScheme",
                //             new XElement(cbc + "CompanyID", company.TaxNumber),
                //             new XElement(cac + "TaxScheme",
                //                 new XElement(cbc + "ID", "VAT")
                //             )
                //         ),
                //         new XElement(cac + "PartyLegalEntity",
                //             new XElement(cbc + "RegistrationName", company.Name)
                //         )
                //     )
                // ),
                new XElement(
                    cac + "Delivery",
                    new XElement(cbc + "ActualDeliveryDate", Date.ToString("yyyy-MM-dd")),
                    new XElement(cbc + "LatestDeliveryDate", Date.ToString("yyyy-MM-dd"))
                ),
                new XElement(
                    cac + "PaymentMeans",
                    new XElement(cbc + "PaymentMeansCode", "10"),
                    new XElement(cbc + "InstructionNote", "Returned items")
                ),
                new XElement(
                    cac + "TaxTotal",
                    new XElement(cbc + "TaxAmount", new XAttribute("currencyID", "SAR"), "0.00"),
                    new XElement(
                        cac + "TaxSubtotal",
                        new XElement(
                            cbc + "TaxableAmount",
                            new XAttribute("currencyID", "SAR"),
                            "250.00"
                        ),
                        new XElement(
                            cbc + "TaxAmount",
                            new XAttribute("currencyID", "SAR"),
                            "0.00"
                        ),
                        new XElement(cac + "TaxCategory",
                            new XElement(cbc + "ID", "S",
                                new XAttribute("schemeID", "UN/ECE 5305"),
                                new XAttribute("schemeAgencyID", "6"
                            )),
                            new XElement(cbc + "Percent", "15"),
                            new XElement(cac + "TaxScheme",
                                new XElement(cbc + "ID", "VAT",
                                    new XAttribute("schemeID", "UN/ECE 5153"),
                                    new XAttribute("schemeAgencyID", "6")
                                )
                            )
                        )

                    // new XElement(
                    //     cac + "TaxCategory",
                    //     new XElement(cbc + "ID", "Z"),
                    //     new XElement(cbc + "Percent", "0"),
                    //     new XElement(cbc + "TaxExemptionReasonCode", "UN/ECE 5305"),
                    //     new XElement(
                    //         cbc + "TaxExemptionReason",
                    //         "Private healthcare to citizen"
                    //     ),
                    //     new XElement(cac + "TaxScheme", new XElement(cbc + "ID", "VAT"))
                    // )
                    )
                ),
                new XElement(
                    cac + "TaxTotal",
                    new XElement(cbc + "TaxAmount", new XAttribute("currencyID", "SAR"), "0.00")
                ),
                new XElement(
                    cac + "LegalMonetaryTotal",
                    new XElement(
                        cbc + "LineExtensionAmount",
                        new XAttribute("currencyID", "SAR"),
                        "250.00"
                    ),
                    new XElement(
                        cbc + "TaxExclusiveAmount",
                        new XAttribute("currencyID", "SAR"),
                        "250.00"
                    ),
                    new XElement(
                        cbc + "TaxInclusiveAmount",
                        new XAttribute("currencyID", "SAR"),
                        "250.00"
                    ),
                    new XElement(
                        cbc + "PayableAmount",
                        new XAttribute("currencyID", "SAR"),
                        "250.00"
                    )
                ),
                new XElement(
                    cac + "InvoiceLine",
                    new XElement(cbc + "ID", "73428"),
                    new XElement(cbc + "InvoicedQuantity", "1"),
                    new XElement(
                        cbc + "LineExtensionAmount",
                        new XAttribute("currencyID", "SAR"),
                        "250.00"
                    ),
                    new XElement(
                        cac + "TaxTotal",
                        new XElement(
                            cbc + "TaxAmount",
                            new XAttribute("currencyID", "SAR"),
                            "0.00"
                        ),
                        new XElement(
                            cbc + "RoundingAmount",
                            new XAttribute("currencyID", "SAR"),
                            "250.00"
                        )
                    ),
                    new XElement(
                        cac + "Item",
                        new XElement(cbc + "Name", "Surgery"),
                        new XElement(
                            cac + "ClassifiedTaxCategory",
                            new XElement(cbc + "ID", "Z"),
                            new XElement(cbc + "Percent", "0"),
                            new XElement(cac + "TaxScheme", new XElement(cbc + "ID", "VAT"))
                        )
                    ),
                    new XElement(
                        cac + "Price",
                        new XElement(
                            cbc + "PriceAmount",
                            new XAttribute("currencyID", "SAR"),
                            "250.00"
                        )
                    )
                )
            )
        );

        // // Save the XML document to a file
        // xmlDoc.Save("Simplified_Debit_Note.xml");

        // Console.WriteLine("XML document created successfully.");
        XmlDocument xmlDoc = new XmlDocument();
        xmlDoc.PreserveWhitespace = true;
        using XmlReader reader = doc.CreateReader();
        xmlDoc.Load(reader);
        LogsFile.MessageZatca($"Create Debit Default");
        return FormatXML.PrettyXml(xmlDoc);
    }

    public XmlDocument CreateXmlCreditDefault(CompanyData company, Guid uuid, string type)
    {
        DateTime Date = DateTime.Now;

        // Create the XML document
        XNamespace ns = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
        XNamespace cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
        XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";

        var doc = new XDocument(
            new XElement(
                ns + "Invoice",
                new XAttribute(XNamespace.Xmlns + "cac", cac),
                new XAttribute(XNamespace.Xmlns + "cbc", cbc),
                new XElement(cbc + "ProfileID", "reporting:1.0"),
                new XElement(cbc + "ID", "935"),
                new XElement(cbc + "UUID", uuid),
                new XElement(cbc + "IssueDate", Date.ToString("yyyy-MM-dd")),
                new XElement(cbc + "IssueTime", Date.ToString("HH:mm:ss")),
                new XElement(cbc + "InvoiceTypeCode", new XAttribute("name", type), "383"),
                new XElement(cbc + "DocumentCurrencyCode", "SAR"),
                new XElement(cbc + "TaxCurrencyCode", "SAR"),
                new XElement(
                    cac + "BillingReference",
                    new XElement(
                        cac + "InvoiceDocumentReference",
                        new XElement(
                            cbc + "ID",
                            $"Invoice Number: 1; Invoice Issue Date: {Date:yyyy-MM-dd}"
                        )
                    )
                ),
                new XElement(
                    cac + "AdditionalDocumentReference",
                    new XElement(cbc + "ID", "ICV"),
                    new XElement(cbc + "UUID", "70")
                ),
                new XElement(
                    cac + "AdditionalDocumentReference",
                    new XElement(cbc + "ID", "PIH"),
                    new XElement(
                        cac + "Attachment",
                        new XElement(
                            cbc + "EmbeddedDocumentBinaryObject",
                            new XAttribute("mimeCode", "text/plain"),
                            HashHelper.ComputeSha256("0")
                        )
                    )
                ),
                new XElement(
                    cac + "AdditionalDocumentReference",
                    new XElement(cbc + "ID", "QR"),
                    new XElement(
                        cac + "Attachment",
                        new XElement(
                            cbc + "EmbeddedDocumentBinaryObject",
                            new XAttribute("mimeCode", "text/plain"),
                            ""
                        )
                    )
                ),
                new XElement(
                    cac + "Signature",
                    new XElement(cbc + "ID", "urn:oasis:names:specification:ubl:signature:Invoice"),
                    new XElement(
                        cbc + "SignatureMethod",
                        "urn:oasis:names:specification:ubl:dsig:enveloped:xades"
                    )
                ),
                new XElement(
                    cac + "AccountingSupplierParty",
                    new XElement(
                        cac + "Party",
                        new XElement(
                            cac + "PartyIdentification",
                            new XElement(cbc + "ID", new XAttribute("schemeID", "CRN"), company.Crn)
                        ),
                        new XElement(
                            cac + "PostalAddress",
                            new XElement(cbc + "StreetName", "الامير سلطان | Prince Sultan"),
                            new XElement(cbc + "BuildingNumber", "2322"),
                            new XElement(cbc + "CitySubdivisionName", "المربع | Al-Murabba"),
                            new XElement(cbc + "CityName", "الرياض | Riyadh"),
                            new XElement(cbc + "PostalZone", "23333"),
                            new XElement(
                                cac + "Country",
                                new XElement(cbc + "IdentificationCode", "SA")
                            )
                        ),
                        new XElement(
                            cac + "PartyTaxScheme",
                            new XElement(cbc + "CompanyID", company.TaxNumber),
                            new XElement(cac + "TaxScheme", new XElement(cbc + "ID", "VAT"))
                        ),
                        new XElement(
                            cac + "PartyLegalEntity",
                            new XElement(cbc + "RegistrationName", company.Name)
                        )
                    )
                ),
                CreateAccountingCustomerParty(type, cac, cbc),
                // new XElement(cac + "AccountingCustomerParty",
                //     new XElement(cac + "Party",
                //         new XElement(cac + "PostalAddress",
                //             new XElement(cbc + "StreetName", "صلاح الدين | Salah Al-Din"),
                //             new XElement(cbc + "BuildingNumber", "1111"),
                //             new XElement(cbc + "CitySubdivisionName", "المروج | Al-Murooj"),
                //             new XElement(cbc + "CityName", "الرياض | Riyadh"),
                //             new XElement(cbc + "PostalZone", "12222"),
                //             new XElement(cac + "Country",
                //                 new XElement(cbc + "IdentificationCode", "SA")
                //             )
                //         ),
                //         new XElement(cac + "PartyTaxScheme",
                //             new XElement(cbc + "CompanyID", company.TaxNumber),
                //             new XElement(cac + "TaxScheme",
                //                 new XElement(cbc + "ID", "VAT")
                //             )
                //         ),
                //         new XElement(cac + "PartyLegalEntity",
                //             new XElement(cbc + "RegistrationName", company.Name)
                //         )
                //     )
                // ),
                new XElement(
                    cac + "Delivery",
                    new XElement(cbc + "ActualDeliveryDate", "2022-03-13"),
                    new XElement(cbc + "LatestDeliveryDate", "2022-03-15")
                ),
                new XElement(
                    cac + "PaymentMeans",
                    new XElement(cbc + "PaymentMeansCode", "10"),
                    new XElement(cbc + "InstructionNote", "Returned items")
                ),
                new XElement(
                    cac + "TaxTotal",
                    new XElement(cbc + "TaxAmount", new XAttribute("currencyID", "SAR"), "0.00"),
                    new XElement(
                        cac + "TaxSubtotal",
                        new XElement(
                            cbc + "TaxableAmount",
                            new XAttribute("currencyID", "SAR"),
                            "250.00"
                        ),
                        new XElement(
                            cbc + "TaxAmount",
                            new XAttribute("currencyID", "SAR"),
                            "0.00"
                        ),

                        new XElement(cac + "TaxCategory",
                            new XElement(cbc + "ID", "S",
                                new XAttribute("schemeID", "UN/ECE 5305"),
                                new XAttribute("schemeAgencyID", "6"
                            )),
                            new XElement(cbc + "Percent", "15"),
                            new XElement(cac + "TaxScheme",
                                new XElement(cbc + "ID", "VAT",
                                    new XAttribute("schemeID", "UN/ECE 5153"),
                                    new XAttribute("schemeAgencyID", "6")
                                )
                            )
                        ))
                ),
                new XElement(
                    cac + "TaxTotal",
                    new XElement(cbc + "TaxAmount", new XAttribute("currencyID", "SAR"), "0.00")
                ),
                new XElement(
                    cac + "LegalMonetaryTotal",
                    new XElement(
                        cbc + "LineExtensionAmount",
                        new XAttribute("currencyID", "SAR"),
                        "250.00"
                    ),
                    new XElement(
                        cbc + "TaxExclusiveAmount",
                        new XAttribute("currencyID", "SAR"),
                        "250.00"
                    ),
                    new XElement(
                        cbc + "TaxInclusiveAmount",
                        new XAttribute("currencyID", "SAR"),
                        "250.00"
                    ),
                    new XElement(
                        cbc + "PayableAmount",
                        new XAttribute("currencyID", "SAR"),
                        "250.00"
                    )
                ),
                new XElement(
                    cac + "InvoiceLine",
                    new XElement(cbc + "ID", "73428"),
                    new XElement(cbc + "InvoicedQuantity", "1"),
                    new XElement(
                        cbc + "LineExtensionAmount",
                        new XAttribute("currencyID", "SAR"),
                        "250.00"
                    ),
                    new XElement(
                        cac + "TaxTotal",
                        new XElement(
                            cbc + "TaxAmount",
                            new XAttribute("currencyID", "SAR"),
                            "0.00"
                        ),
                        new XElement(
                            cbc + "RoundingAmount",
                            new XAttribute("currencyID", "SAR"),
                            "250.00"
                        )
                    ),
                    new XElement(
                        cac + "Item",
                        new XElement(cbc + "Name", "Surgery"),
                        new XElement(
                            cac + "ClassifiedTaxCategory",
                            new XElement(cbc + "ID", "Z"),
                            new XElement(cbc + "Percent", "0"),
                            new XElement(cac + "TaxScheme", new XElement(cbc + "ID", "VAT"))
                        )
                    ),
                    new XElement(
                        cac + "Price",
                        new XElement(
                            cbc + "PriceAmount",
                            new XAttribute("currencyID", "SAR"),
                            "250.00"
                        )
                    )
                )
            )
        );

        // // Save the XML document to a file
        // xmlDoc.Save("Simplified_Debit_Note.xml");

        // Console.WriteLine("XML document created successfully.");
        XmlDocument xmlDoc = new XmlDocument();
        xmlDoc.PreserveWhitespace = true;
        using XmlReader reader = doc.CreateReader();
        xmlDoc.Load(reader);
        LogsFile.MessageZatca($"Create Credit Default");
        return FormatXML.PrettyXml(xmlDoc);
    }

    public XmlDocument CreateXmlDefault(CompanyData company, Guid uuid, string type)
    {
        try
        {
            DateTime Date = DateTime.Now;

            string ident =
                "<Invoice xmlns=\"urn:oasis:names:specification:ubl:schema:xsd:Invoice-2\" xmlns:cac=\"urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2\" xmlns:cbc=\"urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2\" xmlns:ext=\"urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2\"></Invoice>";
            XDocument doc = XDocument.Parse(ident);
            XElement i = doc.Root!;
            XNamespace nsCac = i.GetNamespaceOfPrefix("cac")!;
            XNamespace nsCbc = i.GetNamespaceOfPrefix("cbc")!;

            XAttribute TextPlain = new XAttribute("mimeCode", "text/plain");
            XAttribute currencyID = new XAttribute("currencyID", "SAR");
            XAttribute schemeID05 = new XAttribute("schemeID", "UN/ECE 5305");
            XAttribute schemeID53 = new XAttribute("schemeID", "UN/ECE 5153");
            XAttribute schemeAgencyID = new XAttribute("schemeAgencyID", "6");

            XElement ProfileID = new XElement(nsCbc + "ProfileID", "reporting:1.0");
            XElement ID = new XElement(nsCbc + "ID", 1);
            XElement UUID = new XElement(nsCbc + "UUID", uuid);

            XElement IssueDate = new XElement(nsCbc + "IssueDate", Date.ToString("yyyy-MM-dd"));
            XElement IssueTime = new XElement(nsCbc + "IssueTime", Date.ToString("HH:mm:ss"));

            XElement InvoiceTypeCode = new XElement(
                nsCbc + "InvoiceTypeCode",
                "388",
                new XAttribute("name", type)
            );
            XElement Note = new XElement(nsCbc + "Note", "", new XAttribute("languageID", "ar"));
            XElement DocumentCurrencyCode = new XElement(nsCbc + "DocumentCurrencyCode", "SAR");
            XElement TaxCurrencyCode = new XElement(nsCbc + "TaxCurrencyCode", "SAR");

            XElement ICV = new XElement(
                nsCac + "AdditionalDocumentReference",
                new XElement(nsCbc + "ID", "ICV"),
                new XElement(nsCbc + "UUID", "10")
            );

            XElement PIH = new XElement(
                nsCac + "AdditionalDocumentReference",
                new XElement(nsCbc + "ID", "PIH"),
                new XElement(
                    nsCac + "Attachment",
                    new XElement(
                        nsCbc + "EmbeddedDocumentBinaryObject",
                        "NWZlY2ViNjZmZmM4NmYzOGQ5NTI3ODZjNmQ2OTZjNzljMmRiYzIzOWRkNGU5MWI0NjcyOWQ3M2EyN2ZiNTdlOQ==",
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
                        new XElement(nsCbc + "ID", company.Crn, new XAttribute("schemeID", "CRN"))
                    ),
                    new XElement(
                        nsCac + "PostalAddress",
                        new XElement(nsCbc + "StreetName", "الامير سلطان"),
                        new XElement(nsCbc + "BuildingNumber", "3242"),
                        new XElement(nsCbc + "PlotIdentification", "4323"),
                        new XElement(nsCbc + "CitySubdivisionName", "32423423"),
                        new XElement(nsCbc + "CityName", "الرياض | Riyadh"),
                        new XElement(nsCbc + "PostalZone", "32432"),
                        new XElement(
                            nsCac + "Country",
                            new XElement(nsCbc + "IdentificationCode", "SA")
                        )
                    ),
                    new XElement(
                        nsCac + "PartyTaxScheme",
                        new XElement(nsCbc + "CompanyID", company.TaxNumber),
                        new XElement(nsCac + "TaxScheme", new XElement(nsCbc + "ID", "VAT"))
                    ),
                    new XElement(
                        nsCac + "PartyLegalEntity",
                        new XElement(nsCbc + "RegistrationName", company.Name)
                    )
                )
            );
            XElement AccountingCustomerParty;
            if (type == "0200000")
            {
                AccountingCustomerParty = new XElement(
                               nsCac + "AccountingCustomerParty",
                               new XElement(
                                   nsCac + "Party",
                                   new XElement(
                                       nsCac + "PostalAddress",
                                       new XElement(nsCbc + "StreetName", ""),
                                       new XElement(nsCbc + "CitySubdivisionName", "32423423"),
                                       new XElement(
                                           nsCac + "Country",
                                           new XElement(nsCbc + "IdentificationCode", "SA")
                                       )
                                   ),
                                   new XElement(
                                       nsCac + "PartyTaxScheme",
                                       new XElement(nsCac + "TaxScheme", new XElement(nsCbc + "ID", "VAT"))
                                   ),
                                   new XElement(
                                       nsCac + "PartyLegalEntity",
                                       new XElement(nsCbc + "RegistrationName", "")
                                   )
                               )
                           );
            }
            else
            {
                AccountingCustomerParty = new XElement(
                nsCac + "AccountingCustomerParty",
                new XElement(
                    nsCac + "Party",
                        new XElement(
                            nsCac + "PartyIdentification",
                            new XElement(
                                nsCbc + "ID",
                                new XAttribute("schemeID", "NAT"),
                                "1011125255"
                            )
                        ),
                    new XElement(
                        nsCac + "PostalAddress",
                        new XElement(nsCbc + "StreetName", "صلاح الدين | Salah Al-Din"),
                        new XElement(nsCbc + "BuildingNumber", "1111"),
                        new XElement(nsCbc + "CitySubdivisionName", "المروج | Al-Murooj"),
                        new XElement(nsCbc + "CityName", "الرياض | Riyadh"),
                        new XElement(nsCbc + "PostalZone", "12222"),
                        new XElement(
                            nsCac + "Country",
                            new XElement(nsCbc + "IdentificationCode", "SA")
                        )
                    ),
                    new XElement(
                        nsCac + "PartyTaxScheme",
                        new XElement(nsCbc + "CompanyID", "399999999800003"),
                        new XElement(nsCac + "TaxScheme", new XElement(nsCbc + "ID", "VAT"))
                    ),
                    new XElement(
                        nsCac + "PartyLegalEntity",
                        new XElement(nsCbc + "RegistrationName", "شركة نماذج فاتورة المحدودة | Fatoora Samples LTD")
                    )
                )
            );
            }
            XElement PaymentMeans = new XElement(
                nsCac + "PaymentMeans",
                new XElement(nsCbc + "PaymentMeansCode", "10")
            );

            XElement AllowanceCharge = new XElement(
                nsCac + "AllowanceCharge",
                new XElement(nsCbc + "ChargeIndicator", "false"),
                new XElement(nsCbc + "AllowanceChargeReason", "discount"),
                new XElement(nsCbc + "Amount", "0.00", new XAttribute(currencyID)),
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
            );

            XElement TaxTotal = new XElement(
                nsCac + "TaxTotal",
                new XElement(
                    nsCbc + "TaxAmount",
                    String.Format("{0:00.00}", 15),
                    new XAttribute(currencyID)
                )
            );

            XElement TaxTotalx = new XElement(
                nsCac + "TaxTotal",
                new XElement(
                    nsCbc + "TaxAmount",
                    String.Format("{0:00.00}", 15),
                    new XAttribute(currencyID)
                ),
                new XElement(
                    nsCac + "TaxSubtotal",
                    new XElement(
                        nsCbc + "TaxableAmount",
                        String.Format("{0:00.00}", 100),
                        new XAttribute(currencyID)
                    ),
                    new XElement(
                        nsCbc + "TaxAmount",
                        String.Format("{0:00.00}", 15),
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

            XElement LegalMonetaryTotal = new XElement(
                nsCac + "LegalMonetaryTotal",
                new XElement(
                    nsCbc + "LineExtensionAmount",
                    String.Format("{0:00.00}", 100),
                    new XAttribute(currencyID)
                ),
                new XElement(
                    nsCbc + "TaxExclusiveAmount",
                    String.Format("{0:00.00}", 100),
                    new XAttribute(currencyID)
                ),
                new XElement(
                    nsCbc + "TaxInclusiveAmount",
                    String.Format("{0:00.00}", 115),
                    new XAttribute(currencyID)
                ),
                new XElement(nsCbc + "AllowanceTotalAmount", "0.00", new XAttribute(currencyID)),
                new XElement(nsCbc + "PrepaidAmount", "0.00", new XAttribute(currencyID)),
                new XElement(
                    nsCbc + "PayableAmount",
                    String.Format("{0:00.00}", 115),
                    new XAttribute(currencyID)
                )
            );

            i.Add(ProfileID);
            i.Add(ID);
            i.Add(UUID);
            i.Add(IssueDate);
            i.Add(IssueTime);
            i.Add(InvoiceTypeCode);
            i.Add(Note);
            i.Add(DocumentCurrencyCode);
            i.Add(TaxCurrencyCode);
            i.Add(ICV);
            i.Add(PIH);
            i.Add(AccountingSupplierParty);
            i.Add(AccountingCustomerParty);
            i.Add(PaymentMeans);
            i.Add(AllowanceCharge);
            i.Add(TaxTotal);
            i.Add(TaxTotalx);
            i.Add(LegalMonetaryTotal);

            var price = 100;
            XElement InvoiceLine = new XElement(
                nsCac + "InvoiceLine",
                new XElement(nsCbc + "ID", 1),
                new XElement(nsCbc + "InvoicedQuantity", 1, new XAttribute("unitCode", "PCE")),
                new XElement(
                    nsCbc + "LineExtensionAmount",
                    String.Format("{0:00.00}", 115 / 1.15),
                    new XAttribute(currencyID)
                ),
                new XElement(
                    nsCac + "TaxTotal",
                    new XElement(
                        nsCbc + "TaxAmount",
                        String.Format("{0:00.00}", 115 - (115 / 1.15)),
                        new XAttribute(currencyID)
                    ),
                    new XElement(
                        nsCbc + "RoundingAmount",
                        String.Format("{0:00.00}", 115),
                        new XAttribute(currencyID)
                    )
                ),
                new XElement(
                    nsCac + "Item",
                    new XElement(nsCbc + "Name", "Test"),
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
                        String.Format("{0:00.00}", price),
                        new XAttribute(currencyID)
                    ),
                    new XElement(
                        nsCac + "AllowanceCharge",
                        new XElement(nsCbc + "ChargeIndicator", "false"),
                        new XElement(nsCbc + "AllowanceChargeReason", "discount"),
                        new XElement(nsCbc + "Amount", 0.00, new XAttribute(currencyID))
                    )
                )
            );
            i.Add(InvoiceLine);

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.PreserveWhitespace = true;
            using (XmlReader reader = doc.CreateReader())
            {
                xmlDoc.Load(reader);
            }
            return FormatXML.PrettyXml(xmlDoc);
        }
        catch (Exception ex)
        {
            // Log the exception and handle it appropriately
            throw new ApplicationException(
                "An error occurred while creating the XML document.",
                ex
            );
        }
    }

    XElement CreateAccountingCustomerParty(string type, XNamespace cac, XNamespace cbc)
    {
        XElement CustomerParty;
        if (type == "0200000")
        {
            CustomerParty = new XElement(
                 cac + "AccountingCustomerParty",
                 new XElement(
                     cac + "Party",
                     //  new XElement(
                     //      cac + "PartyIdentification",
                     //      new XElement(
                     //          cbc + "ID",
                     //          new XAttribute("schemeID", "CRN"),
                     //          "1234567890"
                     //      )
                     //  ),
                     new XElement(
                         cac + "PostalAddress",
                         new XElement(cbc + "StreetName", "صلاح الدين | Salah Al-Din"),
                         new XElement(cbc + "BuildingNumber", "1111"),
                         new XElement(cbc + "CitySubdivisionName", "المروج | Al-Murooj"),
                         new XElement(cbc + "CityName", "الرياض | Riyadh"),
                         new XElement(cbc + "PostalZone", "12222"),
                         new XElement(
                             cac + "Country",
                             new XElement(cbc + "IdentificationCode", "SA")
                         )
                     ),
                     new XElement(
                         cac + "PartyTaxScheme",
                         new XElement(cbc + "CompanyID", "399999999800003"),
                         new XElement(cac + "TaxScheme", new XElement(cbc + "ID", "VAT"))
                     ),
                     new XElement(
                         cac + "PartyLegalEntity",
                         new XElement(
                             cbc + "RegistrationName",
                             "شركة نماذج فاتورة المحدودة | Fatoora Samples LTD"
                         )
                     )
                 )
             );

        }
        else
        {
            CustomerParty = new XElement(
                cac + "AccountingCustomerParty",
                new XElement(
                    cac + "Party",
                    // new XElement(
                    //     cac + "PartyIdentification",
                    //     new XElement(
                    //         cbc + "ID",
                    //         new XAttribute("schemeID", "NAT"),
                    //         "1011125255"
                    //     )
                    // ),
                    new XElement(
                        cac + "PostalAddress",
                        new XElement(cbc + "StreetName", "صلاح الدين | Salah Al-Din"),
                        new XElement(cbc + "BuildingNumber", "1111"),
                        new XElement(cbc + "CitySubdivisionName", "المروج | Al-Murooj"),
                        new XElement(cbc + "CityName", "الرياض | Riyadh"),
                        new XElement(cbc + "PostalZone", "12222"),
                        new XElement(
                            cac + "Country",
                            new XElement(cbc + "IdentificationCode", "SA")
                        )
                    ),
                    new XElement(
                        cac + "PartyTaxScheme",
                        new XElement(cbc + "CompanyID", "399999999800003"),
                        new XElement(cac + "TaxScheme", new XElement(cbc + "ID", "VAT"))
                    ),
                    new XElement(
                        cac + "PartyLegalEntity",
                        new XElement(
                            cbc + "RegistrationName",
                            "شركة نماذج فاتورة المحدودة | Fatoora Samples LTD"
                        )
                    )
                )
            );
        }
        return CustomerParty;
    }

}
