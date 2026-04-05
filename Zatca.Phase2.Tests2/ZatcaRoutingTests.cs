using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Xunit;
using Zatca_Phase_II.Eum;
using Zatca_Phase_II.Interfaces;
using Zatca_Phase_II.Models;
using Zatca_Phase_II.Services;

namespace Zatca.Phase2.Tests2;

public class ZatcaRoutingTests
{
    private readonly Mock<IEInvoiceService> _mockEInvoiceService;
    private readonly Mock<IOutboxEInvoiceService> _mockOutboxService;
    private readonly ZatcaRoutingService _sut;

    public ZatcaRoutingTests()
    {
        _mockEInvoiceService = new Mock<IEInvoiceService>();
        _mockOutboxService = new Mock<IOutboxEInvoiceService>();
        _sut = new ZatcaRoutingService(_mockEInvoiceService.Object, _mockOutboxService.Object);
    }

    private Bill CreateValidBill(string? taxNumber)
    {
        return new Bill
        {
            SupTotal = 100,
            TotalVAT = 15,
            NetTotal = 115,
            Customer = new CustomerORSupplier { TaxNumber = taxNumber },
            BillDetails = new List<BillDetails>
            {
                new BillDetails { NetTotal = 100, VATTotal = 15 }
            },
            NoticeType = NoticeType.Regular,
            OrderNo = 1
        };
    }

    [Fact]
    public async Task ProcessInvoiceAsync_B2C_RoutesToOutbox()
    {
        // Test 1: B2C (No VAT) -> enqueue for reporting
        var bill = CreateValidBill(null); // No VAT
        var branch = new ZatcaBranch { 
            RequestID = "", Csr = "", PrivateKey = "", SecretKey = "", BinarySecurityToken = "" 
        };
        
        var dummyFatoora = new Fatoora { Base64QR = "QR123", ObjRequest = new object() };
        _mockEInvoiceService.Setup(s => s.EInvoice(bill, branch, EnvironmentTyp.Simulation))
                            .ReturnsAsync(dummyFatoora);

        var result = await _sut.ProcessInvoiceAsync(bill, branch, EnvironmentTyp.Simulation);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsB2B);
        Assert.Equal("QR123", result.Base64QR);
        
        _mockOutboxService.Verify(
            s => s.EnqueueAsync(bill, branch, EnvironmentTyp.Simulation, It.IsAny<CancellationToken>()), 
            Times.Once);
            
        _mockEInvoiceService.Verify(
            s => s.ClearanceEInvoice(It.IsAny<object>(), It.IsAny<ZatcaBranch>(), It.IsAny<EnvironmentTyp>()), 
            Times.Never);
    }

    [Fact]
    public async Task ProcessInvoiceAsync_B2B_SynchronousClearance()
    {
        // Test 2: B2B (Valid 15-digit VAT) -> wait for Cleared XML
        var bill = CreateValidBill("300000000000003");
        var branch = new ZatcaBranch { 
            RequestID = "", Csr = "", PrivateKey = "", SecretKey = "", BinarySecurityToken = "" 
        };
        var reqObj = new object();
        var dummyFatoora = new Fatoora { Base64QR = "IgnoredLocalQR", ObjRequest = reqObj };
        
        var uploadRes = new TestUploadResponse 
        { 
            HttpStatus = System.Net.HttpStatusCode.OK,
            ClearedInvoice = "PHhtbD5jbGVhcmVkPC94bWw+" // Base64 of <xml>cleared</xml>
        };

        _mockEInvoiceService.Setup(s => s.EInvoice(bill, branch, EnvironmentTyp.Simulation))
                            .ReturnsAsync(dummyFatoora);
                            
        _mockEInvoiceService.Setup(s => s.ClearanceEInvoice(reqObj, branch, EnvironmentTyp.Simulation))
                            .ReturnsAsync(uploadRes);

        var result = await _sut.ProcessInvoiceAsync(bill, branch, EnvironmentTyp.Simulation);

        Assert.True(result.IsSuccess);
        Assert.True(result.IsB2B);
        Assert.Equal("PHhtbD5jbGVhcmVkPC94bWw+", result.ClearedXml);
        
        _mockEInvoiceService.Verify(
            s => s.ClearanceEInvoice(reqObj, branch, EnvironmentTyp.Simulation), 
            Times.Once);

        _mockOutboxService.Verify(
            s => s.EnqueueAsync(It.IsAny<Bill>(), It.IsAny<ZatcaBranch>(), It.IsAny<EnvironmentTyp>(), It.IsAny<CancellationToken>()), 
            Times.Never);
    }

    [Fact]
    public void ProcessInvoiceAsync_InvalidMath_TriggersValidation()
    {
        // Test 3: Math mismatch triggers validation error before any API calls
        var bill = new Bill
        {
            SupTotal = 100,
            TotalVAT = 15,
            NetTotal = 150, // Mismatch! Should fail validation
            Customer = new CustomerORSupplier { TaxNumber = null },
            BillDetails = new List<BillDetails>
            {
                new BillDetails { NetTotal = 90, VATTotal = 10 }
            }
        };

        var result = ZatcaPreValidation.Validate(bill);

        Assert.False(result.IsSuccess);
        Assert.Contains("Line Items NetTotal sum (90) does not match Invoice SupTotal (100)", result.ErrorMessage);
    }
}
