﻿using DiscordChatGPT.Daemon.Options;
using DiscordChatGPT.Models;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RestSharp;
using System.Net;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace DiscordChatGPT.Services;

public class OpenAiAccessor
{
    private OpenAiOptions _options;

    public OpenAiAccessor(IOptionsMonitor<OpenAiOptions> options)
    {
        _options = options.CurrentValue;
        options.OnChange(OnOptionsChange);
    }

    private void OnOptionsChange(OpenAiOptions newOptions)
    {
        _options = newOptions;
    }

    /// <summary>
    ///     The method uses the RestClient class to send a request to the ChatGPT API, passing the user's message as the
    ///     prompt and sends the response into the Chat
    /// </summary>
    /// <param name="message"></param>
    /// <returns>Boolean indicating whether the request was successful</returns>
    public async Task<(bool success, ChatGPTMessage responseMessage)> ChatGpt(IList<ChatGPTMessage> messages)
    {
        // Create a new RestClient instance
        var client = new RestClient(_options.ChatGptApiUrl);

        // Create a new RestRequest instance
        var request = new RestRequest("", Method.Post);

        // Set the request headers
        request.AddHeader("Content-Type", "application/json");
        request.AddHeader("Authorization", $"Bearer {_options.ApiKey}");

        // Create the request data
        var data = new
        {
            model = "gpt-4",
            messages
        };

        // Serialzie it via JsonSerializer
        var jsonDataString = JsonConvert.SerializeObject(data);

        // Add the request data to the request body
        request.AddJsonBody(jsonDataString);

        // Send the request and get the response
        var response = await client.ExecuteAsync(request);

        // Holds the response from the API.
        string responseText;
        var success = true;
        // Check the status code of the response
        if (response.Content != null && response.StatusCode == HttpStatusCode.OK)
        {
            // Get the response text from the API
            responseText =
                JsonConvert.DeserializeObject<dynamic>(response.Content)?["choices"][0]["message"]["content"] ??
                "Could not deserialize response from ChatGPT API!";
        }
        else
        {
            // Get the ErrorMessage from the API
            responseText = response.ErrorMessage ?? "Unknown error occurred";
            success = false;
        }

        var returnMessage = new ChatGPTMessage
        {
            Role = "assistant",
            Content = responseText.TrimStart('\n'),
            Timestamp = DateTime.UtcNow
        };

        return new(success, returnMessage);
    }

    /// <summary>
    ///     The method uses the RestClient class to send a request to the Dall-E API, passing the user's message as the
    ///     prompt and sends an image to the Chat
    /// </summary>
    /// <param name="message"></param>
    /// <returns>Boolean indicating whether the request was successful</returns>
    public async Task<Tuple<bool, string>> DallE(string message)
    {
        // Create a new RestClient instance
        var client = new RestClient(_options.DalleApiUrl);

        // Create a new RestRequest instance
        var request = new RestRequest("", Method.Post);

        // Set the request headers
        request.AddHeader("Content-Type", "application/json");
        request.AddHeader("Authorization", $"Bearer {_options.ApiKey}");

        // Create the request data
        var data = new
        {
            // The prompt is everything after the !image command
            //model = "image-alpha-001",
            prompt = message,
            n = 1,
            size = "1024x1024"
        };

        // Serialzie it via JsonSerializer
        var jsonData = JsonSerializer.Serialize(data);

        // Add the request data to the request body
        request.AddJsonBody(jsonData);

        // Send the request and get the response
        var response = await client.ExecuteAsync(request);

        // Holds the response from the API.
        string responseText;
        var success = false;
        // Check the status code of the response
        if (response.Content != null && response.StatusCode == HttpStatusCode.OK)
        {
            // Get the image URL from the API response
            var imageUrl = JsonConvert.DeserializeObject<dynamic>(response.Content)?["data"][0]["url"];
            responseText = $"Here is the generated image: {imageUrl}";
            success = true;
        }
        else
        {
            // Get the ErrorMessage from the API
            responseText = response.ErrorMessage ?? string.Empty;
        }

        return new Tuple<bool, string>(success, responseText);
    }
}