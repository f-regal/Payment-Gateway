using System.Text.Json;

using PaymentGateway.Api.Models.Bank;

namespace PaymentGateway.Api.Services;

public class BankClient : IBankClient
{
    private readonly HttpClient _httpClient;

    public BankClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<BankPaymentResponse> ProcessPaymentAsync(BankPaymentRequest request, CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response;

        try
        {
            response = await _httpClient.PostAsJsonAsync("payments", request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new BankUnavailableException("Could not reach the acquiring bank.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new BankUnavailableException("The acquiring bank did not respond in time.", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new BankUnavailableException($"The acquiring bank returned status code {(int)response.StatusCode}.");
            }

            try
            {
                var bankResponse = await response.Content.ReadFromJsonAsync<BankPaymentResponse>(cancellationToken: cancellationToken);

                return bankResponse ?? throw new BankUnavailableException("The acquiring bank returned an empty response.");
            }
            catch (JsonException ex)
            {
                throw new BankUnavailableException("The acquiring bank returned an invalid response.", ex);
            }
        }
    }
}
