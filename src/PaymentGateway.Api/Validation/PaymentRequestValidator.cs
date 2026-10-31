using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Validation;

public class PaymentRequestValidator
{
    private static readonly string[] SupportedCurrencies = { "GBP", "USD", "EUR" };

    private readonly TimeProvider _timeProvider;

    public PaymentRequestValidator(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public IReadOnlyList<string> Validate(PostPaymentRequest request)
    {
        var errors = new List<string>();

        ValidateCardNumber(request.CardNumber, errors);
        ValidateExpiry(request.ExpiryMonth, request.ExpiryYear, errors);
        ValidateCurrency(request.Currency, errors);
        ValidateAmount(request.Amount, errors);
        ValidateCvv(request.Cvv, errors);

        return errors;
    }

    private static void ValidateCardNumber(string? cardNumber, List<string> errors)
    {
        if (string.IsNullOrEmpty(cardNumber))
        {
            errors.Add("Card number is required.");
            return;
        }

        if (cardNumber.Length is < 14 or > 19)
        {
            errors.Add("Card number must be between 14 and 19 characters long.");
        }

        if (!IsDigitsOnly(cardNumber))
        {
            errors.Add("Card number must only contain numeric characters.");
        }
    }

    private void ValidateExpiry(int? month, int? year, List<string> errors)
    {
        if (month is null)
        {
            errors.Add("Expiry month is required.");
        }
        else if (month is < 1 or > 12)
        {
            errors.Add("Expiry month must be between 1 and 12.");
        }

        if (year is null)
        {
            errors.Add("Expiry year is required.");
        }

        if (month is >= 1 and <= 12 && year is not null)
        {
            var today = _timeProvider.GetUtcNow();
            var hasExpired = year < today.Year || (year == today.Year && month < today.Month);

            if (hasExpired)
            {
                errors.Add("Card expiry date must be in the future.");
            }
        }
    }

    private static void ValidateCurrency(string? currency, List<string> errors)
    {
        if (string.IsNullOrEmpty(currency))
        {
            errors.Add("Currency is required.");
            return;
        }

        if (currency.Length != 3)
        {
            errors.Add("Currency must be 3 characters long.");
            return;
        }

        if (!SupportedCurrencies.Contains(currency))
        {
            errors.Add($"Currency must be one of: {string.Join(", ", SupportedCurrencies)}.");
        }
    }

    private static void ValidateAmount(int? amount, List<string> errors)
    {
        if (amount is null)
        {
            errors.Add("Amount is required.");
        }
        else if (amount <= 0)
        {
            errors.Add("Amount must be greater than zero.");
        }
    }

    private static void ValidateCvv(string? cvv, List<string> errors)
    {
        if (string.IsNullOrEmpty(cvv))
        {
            errors.Add("CVV is required.");
            return;
        }

        if (cvv.Length is < 3 or > 4)
        {
            errors.Add("CVV must be 3 or 4 characters long.");
        }

        if (!IsDigitsOnly(cvv))
        {
            errors.Add("CVV must only contain numeric characters.");
        }
    }

    private static bool IsDigitsOnly(string value) => value.All(char.IsAsciiDigit);
}
