using System.Dynamic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace PosWebApi.Tests.Fixtures
{
    /// <summary>
    /// Deserializes HTTP response content using Newtonsoft.Json, matching the API's own
    /// serializer (Program.cs calls AddNewtonsoftJson) so dynamic member access on JSON
    /// objects (e.g. content.items) works as it does with JObject.
    /// </summary>
    public static class HttpContentJsonExtensions
    {
        public static async Task<T?> ReadAsAsync<T>(this HttpContent content)
        {
            var json = await content.ReadAsStringAsync();

            // ReadAsAsync<dynamic>() erases T to object. JsonConvert.DeserializeObject<object>
            // would return JObject/JValue, which themselves implement
            // IDynamicMetaObjectProvider and hijack dynamic dispatch - so a FluentAssertions
            // call like `content.itemCount.Should()` fails with "JValue does not contain a
            // definition for 'Should'". ExpandoObject stores plain CLR primitives instead, so
            // dynamic member access yields real ints/strings that resolve extension methods normally.
            if (typeof(T) == typeof(object))
            {
                return (T?)(object?)JsonConvert.DeserializeObject<ExpandoObject>(json, new ExpandoObjectConverter());
            }

            return JsonConvert.DeserializeObject<T>(json);
        }
    }
}
