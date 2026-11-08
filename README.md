# Instructions for candidates

This is the .NET version of the Payment Gateway challenge. If you haven't already read this [README.md](https://github.com/cko-recruitment/) on the details of this exercise, please do so now. 

## Template structure
```
src/
    PaymentGateway.Api - a skeleton ASP.NET Core Web API
test/
    PaymentGateway.Api.Tests - an empty xUnit test project
imposters/ - contains the bank simulator configuration. Don't change this

.editorconfig - don't change this. It ensures a consistent set of rules for submissions when reformatting code
docker-compose.yml - configures the bank simulator
PaymentGateway.sln
```

Feel free to change the structure of the solution, use a different test library etc.
## Design decisions

### Unreadable request bodies use ASP.NET's default 400 response

The gateway's own validation returns `400 Bad Request` with the payment status and a list of errors:

```json
{ "status": "Rejected", "errors": ["CVV is required."] }
```

This covers every business rule: missing fields, card number length, expiry date, currency, amount and CVV. All request fields are nullable, so a missing field reaches the validator and gets a clear message.

If the body cannot be read as JSON at all (malformed JSON, or a value of the wrong type such as `"amount": 10.5`), ASP.NET rejects the request before it reaches the controller. It returns its standard `ValidationProblemDetails` 400 response instead of the shape above. No payment is created and the bank is not called, so the outcome is the same as a rejection.

I kept the default rather than adding custom handling so the gateway stays simple. A production API would map these errors to the same `Rejected` shape, so clients only need to handle one error format.
