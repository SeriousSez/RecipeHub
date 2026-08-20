using RecipeHub.Domain.Models;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RecipeHub.Api.Converters
{
    public class ImageViewModelJsonConverter : JsonConverter<ImageViewModel>
    {
        public override ImageViewModel Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                reader.GetString();
                return null;
            }

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("Invalid Image value.");
            }

            using var document = JsonDocument.ParseValue(ref reader);
            var root = document.RootElement;

            string url = null;
            string caption = null;

            if (root.TryGetProperty("url", out var urlProp) || root.TryGetProperty("Url", out urlProp))
            {
                url = urlProp.GetString();
            }

            if (root.TryGetProperty("caption", out var captionProp) || root.TryGetProperty("Caption", out captionProp))
            {
                caption = captionProp.GetString();
            }

            if (string.IsNullOrWhiteSpace(url) && string.IsNullOrWhiteSpace(caption))
            {
                return null;
            }

            return new ImageViewModel
            {
                Url = url,
                Caption = caption
            };
        }

        public override void Write(Utf8JsonWriter writer, ImageViewModel value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, options);
        }
    }
}