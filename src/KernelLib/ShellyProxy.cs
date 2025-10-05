using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace KernelLib;

internal class ShellyProxy(string hostName)
{
    HttpClient httpClient = new() { BaseAddress = new Uri(hostName) };

    public bool Switch(bool on)
    {
        var response = httpClient.GetAsync($"/rpc/Switch.Set?id=0&on={on.ToString().ToLower()}").Result;
        if (response.IsSuccessStatusCode)
        {
            var content = response.Content.ReadAsStringAsync().Result;
            var status = System.Text.Json.JsonSerializer.Deserialize<ToggleResponse>(content);
            return status.WasOn;
        }
        else
        {
            throw new Exception($"Request failed with status code: {response.StatusCode}");
        }
    }

    public bool Toggle()
    {
        var response = httpClient.GetAsync("/rpc/Switch.Toggle?id=0").Result;
        if (response.IsSuccessStatusCode)
        {
            var content = response.Content.ReadAsStringAsync().Result;
            var status = System.Text.Json.JsonSerializer.Deserialize<ToggleResponse>(content);
            return status.WasOn;
        }
        else
        {
            throw new Exception($"Request failed with status code: {response.StatusCode}");
        }
    }

    public bool Stato()
    {
        var response = httpClient.GetAsync("/rpc/Switch.GetStatus?id=0").Result;
        if (response.IsSuccessStatusCode)
        {
            var content = response.Content.ReadAsStringAsync().Result;
            var status = System.Text.Json.JsonSerializer.Deserialize<ShellyStatus>(content);
            return status.Output;
        }
        else
        {
            throw new Exception($"Request failed with status code: {response.StatusCode}");
        }
    }

    public class ShellyStatus
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("source")]
        public string Source { get; set; }

        [JsonPropertyName("output")]
        public bool Output { get; set; }

        [JsonPropertyName("temperature")]
        public TemperatureInfo Temperature { get; set; }
    }

    public class TemperatureInfo
    {
        [JsonPropertyName("tC")]
        public double Celsius { get; set; }

        [JsonPropertyName("tF")]
        public double Fahrenheit { get; set; }
    }

    public class ToggleResponse
    {
        [JsonPropertyName("was_on")]
        public bool WasOn { get; set; }
    }
}
