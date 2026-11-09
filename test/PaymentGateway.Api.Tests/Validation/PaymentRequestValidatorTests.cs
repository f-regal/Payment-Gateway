using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Validation;

namespace PaymentGateway.Api.Tests.Validation;

public class PaymentRequestValidatorTests
{
    private static readonly DateTimeOffset Today = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly PaymentRequestValidator _validator = new(new FixedTimeProvider(Today));

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
    public void ValidRequest_HasNoErrors()
    {
        var errors = _validator.Validate(ValidRequest());

        Assert.Empty(errors);
    }

    [Fact]
    public void EmptyRequest_ReportsEveryRequiredField()
    {
        var errors = _validator.Validate(new PostPaymentRequest());

        Assert.Equal(
            new[]
            {
                "Card number is required.",
                "Expiry month is required.",
                "Expiry year is required.",
                "Currency is required.",
                "Amount is required.",
                "CVV is required."
            },
            errors);
    }

    [Theory]
    [InlineData("22224053432488")]
    [InlineData("2222405343248877123")]
    public void CardNumber_WithValidLength_IsAccepted(string cardNumber)
    {
        var request = ValidRequest();
        request.CardNumber = cardNumber;

        Assert.Empty(_validator.Validate(request));
    }

    [Theory]
    [InlineData("2222405343248")]
    [InlineData("22224053432488771234")]
    public void CardNumber_WithInvalidLength_IsRejected(string cardNumber)
    {
        var request = ValidRequest();
        request.CardNumber = cardNumber;

        Assert.Contains("Card number must be between 14 and 19 characters long.", _validator.Validate(request));
    }

    [Theory]
    [InlineData("2222-4053-4324-8877")]
    [InlineData("2222 4053 4324 8877")]
    [InlineData("222240534324887A")]
    public void CardNumber_WithNonNumericCharacters_IsRejected(string cardNumber)
    {
        var request = ValidRequest();
        request.CardNumber = cardNumber;

        Assert.Contains("Card number must only contain numeric characters.", _validator.Validate(request));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(-1)]
    public void ExpiryMonth_OutOfRange_IsRejected(int month)
    {
        var request = ValidRequest();
        request.ExpiryMonth = month;

        Assert.Equal(new[] { "Expiry month must be between 1 and 12." }, _validator.Validate(request));
    }

    [Theory]
    [InlineData(6, 2026)]
    [InlineData(7, 2026)]
    [InlineData(1, 2027)]
    public void Expiry_InCurrentOrFutureMonth_IsAccepted(int month, int year)
    {
        var request = ValidRequest();
        request.ExpiryMonth = month;
        request.ExpiryYear = year;

        Assert.Empty(_validator.Validate(request));
    }

    [Theory]
    [InlineData(5, 2026)]
    [InlineData(12, 2025)]
    public void Expiry_InPast_IsRejected(int month, int year)
    {
        var request = ValidRequest();
        request.ExpiryMonth = month;
        request.ExpiryYear = year;

        Assert.Equal(new[] { "Card expiry date must be in the future." }, _validator.Validate(request));
    }

    [Theory]
    [InlineData("GBP")]
    [InlineData("USD")]
    [InlineData("EUR")]
    public void Currency_Supported_IsAccepted(string currency)
    {
        var request = ValidRequest();
        request.Currency = currency;

        Assert.Empty(_validator.Validate(request));
    }

    [Theory]
    [InlineData("GB")]
    [InlineData("GBPP")]
    public void Currency_WithWrongLength_IsRejected(string currency)
    {
        var request = ValidRequest();
        request.Currency = currency;

        Assert.Equal(new[] { "Currency must be 3 characters long." }, _validator.Validate(request));
    }

    [Theory]
    [InlineData("JPY")]
    [InlineData("gbp")]
    public void Currency_Unsupported_IsRejected(string currency)
    {
        var request = ValidRequest();
        request.Currency = currency;

        Assert.Equal(new[] { "Currency must be one of: GBP, USD, EUR." }, _validator.Validate(request));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Amount_ZeroOrNegative_IsRejected(int amount)
    {
        var request = ValidRequest();
        request.Amount = amount;

        Assert.Equal(new[] { "Amount must be greater than zero." }, _validator.Validate(request));
    }

    [Theory]
    [InlineData("123")]
    [InlineData("1234")]
    [InlineData("012")]
    public void Cvv_Valid_IsAccepted(string cvv)
    {
        var request = ValidRequest();
        request.Cvv = cvv;

        Assert.Empty(_validator.Validate(request));
    }

    [Theory]
    [InlineData("12")]
    [InlineData("12345")]
    public void Cvv_WithInvalidLength_IsRejected(string cvv)
    {
        var request = ValidRequest();
        request.Cvv = cvv;

        Assert.Equal(new[] { "CVV must be 3 or 4 characters long." }, _validator.Validate(request));
    }

    [Theory]
    [InlineData("12a")]
    [InlineData("1 23")]
    public void Cvv_WithNonNumericCharacters_IsRejected(string cvv)
    {
        var request = ValidRequest();
        request.Cvv = cvv;

        Assert.Equal(new[] { "CVV must only contain numeric characters." }, _validator.Validate(request));
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedTimeProvider(DateTimeOffset now) => _now = now;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
