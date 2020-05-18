﻿using Cybtans.Serialization;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Cybtans.Web
{
    public class BinaryInputFormatter : InputFormatter
    {        
        public BinaryInputFormatter()
        {
            SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse(BinarySerializer.MEDIA_TYPE));          
        }

        public override bool CanRead(InputFormatterContext context)
        {
            var request = context.HttpContext.Request;
            return request.Method == "POST" || request.Method == "PUT";
        }        

        public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context)
        {
            var type = context.ModelType;
            var request = context.HttpContext.Request;            

            using MemoryStream stream = new MemoryStream();
            await request.Body.CopyToAsync(stream);
            stream.Position = 0;

            var serializer = new BinarySerializer();
            object result = serializer.Deserialize(stream, type);
            return await InputFormatterResult.SuccessAsync(result);

        }
    }
}
