using PaymentGateway.Api.Exceptions;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Bank;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Services;
using PaymentGateway.Api.Tests.Fakes;
using PaymentGateway.Api.Validation;

namespace PaymentGateway.Api.Tests.Services;

public class PaymentServiceTests
{
    private static readonly DateTimeOffset Today = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeBankClient _bankClient = new();
    private readonly RecordingPaymentsRepository _repository = new();
    private readonly PaymentService _service;

    public PaymentServiceTests()
    {
        var validator = new PaymentRequestValidator(new FixedTimeProvider(Today));
        _service = new PaymentService(validator, _bankClient, _repository);
    }

    private static PostPaymentRequest ValidRequest() => new()
    {
        CardNumber = "2222405343248877",
        ExpiryMonth = 4,
        ExpiryYear = 2027,
        Currency = "GBP",
        Amount = 100,
        Cvv = "123"
    };

    [Fact]
    public async Task ReturnsAuthorized_WhenBankAuthorizes()
    {
        _bankClient.Response = new BankPaymentResponse(Authorized: true, AuthorizationCode: "auth-code");

        var result = await _service.ProcessPaymentAsync(ValidRequest());

        Assert.False(result.IsRejected);
        Assert.NotNull(result.Payment);
        Assert.Equal(PaymentStatus.Authorized, result.Payment!.Status);
        Assert.NotEqual(Guid.Empty, result.Payment.Id);
        Assert.Equal("8877", result.Payment.CardNumberLastFour);
        Assert.Equal(4, result.Payment.ExpiryMonth);
        Assert.Equal(2027, result.Payment.ExpiryYear);
        Assert.Equal("GBP", result.Payment.Currency);
        Assert.Equal(100, result.Payment.Amount);
    }

    [Fact]
    public async Task ReturnsDeclined_WhenBankDeclines()
    {
        _bankClient.Response = new BankPaymentResponse(Authorized: false, AuthorizationCode: "");

        var result = await _service.ProcessPaymentAsync(ValidRequest());

        Assert.False(result.IsRejected);
        Assert.Equal(PaymentStatus.Declined, result.Payment!.Status);
    }

    [Theory]
    [InlineData(true, PaymentStatus.Authorized)]
    [InlineData(false, PaymentStatus.Declined)]
    public async Task StoresProcessedPayment(bool authorized, PaymentStatus expectedStatus)
    {
        _bankClient.Response = new BankPaymentResponse(authorized, AuthorizationCode: null);

        var result = await _service.ProcessPaymentAsync(ValidRequest());

        var stored = Assert.Single(_repository.Added);
        Assert.Equal(result.Payment!.Id, stored.Id);
        Assert.Equal(expectedStatus, stored.Status);
        Assert.Equal("8877", stored.CardNumberLastFour);
    }

    [Fact]
    public async Task SendsFullCardDetailsToBank()
    {
        await _service.ProcessPaymentAsync(ValidRequest());

        var sent = Assert.Single(_bankClient.Requests);
        Assert.Equal("2222405343248877", sent.CardNumber);
        Assert.Equal("04/2027", sent.ExpiryDate);
        Assert.Equal("GBP", sent.Currency);
        Assert.Equal(100, sent.Amount);
        Assert.Equal("123", sent.Cvv);
    }

    [Fact]
    public async Task ReturnsRejected_WithoutCallingBankOrStoring_WhenRequestIsInvalid()
    {
        var request = ValidRequest();
        request.Currency = "JPY";
        request.Cvv = null;

        var result = await _service.ProcessPaymentAsync(request);

        Assert.True(result.IsRejected);
        Assert.Null(result.Payment);
        Assert.Equal(2, result.Errors.Count);
        Assert.Empty(_bankClient.Requests);
        Assert.Empty(_repository.Added);
    }

    [Fact]
    public async Task PropagatesBankUnavailable_WithoutStoring()
    {
        _bankClient.Exception = new BankUnavailableException("Bank down");

        await Assert.ThrowsAsync<BankUnavailableException>(() => _service.ProcessPaymentAsync(ValidRequest()));

        Assert.Empty(_repository.Added);
    }

    [Fact]
    public async Task GivesEachPaymentAUniqueId()
    {
        var first = await _service.ProcessPaymentAsync(ValidRequest());
        var second = await _service.ProcessPaymentAsync(ValidRequest());

        Assert.NotEqual(first.Payment!.Id, second.Payment!.Id);
    }

    private sealed class RecordingPaymentsRepository : IPaymentsRepository
    {
        public List<Payment> Added { get; } = new();

        public void Add(Payment payment) => Added.Add(payment);

        public Payment? Get(Guid id) => Added.FirstOrDefault(p => p.Id == id);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedTimeProvider(DateTimeOffset now) => _now = now;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
