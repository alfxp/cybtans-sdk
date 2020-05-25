﻿#nullable enable

using Cybtans.Proto.AST;
using Cybtans.Proto.Utils;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Markup;

namespace Cybtans.Proto.Generators.CSharp
{
    public class TypeGeneratorOption : OutputOption
    {
        public bool PartialClass { get; set; } = true;

        public bool GenerateAccesor { get; set; } = true;
    }

    public class TypeGenerator : FileGenerator
    {           
        public TypeGenerator(ProtoFile proto, TypeGeneratorOption option):base(proto, option)
        {
            Namespace = $"{proto.Option.Namespace}.{option.Namespace ?? "Models"}";
        }

        public Dictionary<EnumDeclaration, EnumInfo> Enums { get; } = new Dictionary<EnumDeclaration, EnumInfo>();

        public Dictionary<ITypeDeclaration, MessageClassInfo> Messages { get; } = new Dictionary<ITypeDeclaration, MessageClassInfo>();

        public string Namespace { get; set; }

        public override void GenerateCode()
        {
            Directory.CreateDirectory(_option.OutputDirectory);

            CsFileWriter? enumWriter = null;
            bool hasEnums = _proto.Declarations.Any(x => x is EnumDeclaration);
            if (hasEnums)
            {
                var enumInfo = new EnumInfo((EnumDeclaration)_proto.Declarations.First(x => x is EnumDeclaration), _option, _proto);
                enumWriter = CreateWriter(enumInfo.Namespace);                
            }

            foreach (var item in _proto.Declarations)
            {
                if (item is MessageDeclaration msg)
                {                    
                    var info = new MessageClassInfo(msg, _option, _proto);

                    GenerateMessage(info);                

                    Messages.Add(msg, info);
                }

                else if (item is EnumDeclaration e && enumWriter!=null)
                {
                    var info = new EnumInfo(e, _option, _proto);
                    
                    GenerateEnum(info, enumWriter.Class);

                    Enums.Add(e, info);
                }
            }

            if (enumWriter != null)
            {
                enumWriter.Save("Enums");               
            }
        }
     
        private void GenerateEnum(EnumInfo info, CodeWriter clsWriter)
        {                     
            clsWriter.Append("public ");
            clsWriter.Append($"enum {info.Name} ").AppendLine();

            clsWriter.Append("{").AppendLine();
            clsWriter.Append('\t', 1);

            var bodyWriter = clsWriter.Block($"BODY_{info.Name}");

            foreach (var item in info.Fields.Values.OrderBy(x=>x.Field.Value))
            {                               
                bodyWriter.Append(item.Name).Append(" = ").Append(item.Field.Value.ToString()).Append(",");
                bodyWriter.AppendLine();
                bodyWriter.AppendLine();
            }
            
            clsWriter.Append("}").AppendLine();            
        }

        private void GenerateMessage(MessageClassInfo info)
        {
            var writer = CreateWriter(info.Namespace);

            var clsWriter = writer.Class;
            var usingWriter = writer.Usings;

            MessageDeclaration msg = info.Message;
        
            clsWriter.Append("public ");

            if (_option.PartialClass)
            {
                clsWriter.Append("partial ");
            }

            clsWriter.Append($"class {info.Name} ");

            if (msg.Option.Base != null)
            {
                clsWriter.Append($": {msg.Option.Base}");
                if (_option.GenerateAccesor)
                {
                    usingWriter.Append("using Cybtans.Serialization;").AppendLine();
                    clsWriter.Append(", IReflectorMetadataProvider");
                }
            }
            else if (_option.GenerateAccesor)
            {
                usingWriter.Append("using Cybtans.Serialization;").AppendLine();
                clsWriter.Append(": IReflectorMetadataProvider");
            }
           
            clsWriter.AppendLine();
            clsWriter.Append("{").AppendLine();
            clsWriter.Append('\t', 1);

            var bodyWriter = clsWriter.Block("BODY");

            if(msg.Fields.Any(x=>x.Type.IsMap || x.Type.IsArray)) 
            {
                usingWriter.Append("using System.Collections.Generic;").AppendLine();
            }

            if (msg.Fields.Any(x => x.Option.Required)) 
            {
                usingWriter.Append("using System.ComponentModel.DataAnnotations;").AppendLine();
            }

            if (_option.GenerateAccesor)
            {
                bodyWriter.Append($"private static readonly {info.Name}Accesor __accesor = new {info.Name}Accesor();")
                    .AppendLine()
                    .AppendLine();
            }

            foreach (var fieldInfo in info.Fields.Values.OrderBy(x=>x.Field.Number))
            {
                var field = fieldInfo.Field;
                
                if(field.Option.Required)
                {
                    bodyWriter.Append("[Required]").AppendLine();
                }

                if (field.Option.Deprecated)
                {
                    bodyWriter.Append("[Obsolete]").AppendLine();
                }
               
                bodyWriter
                    .Append("public ")                    
                    .Append(fieldInfo.Type);
                
                if (field.Option.Optional && field.Type.TypeDeclaration.Nullable)
                { 
                    //check is the type is nullable
                    bodyWriter.Append("?");
                }
                
                bodyWriter.Append($" {fieldInfo.Name} {{get; set;}}");
                
                if(field.Option.Default != null)
                {
                    bodyWriter.Append(" = ").Append(field.Option.Default.ToString()).Append(";");
                }

                bodyWriter.AppendLine();
                bodyWriter.AppendLine();
            }

            if (_option.GenerateAccesor)
            {
                bodyWriter.Append("public IReflectorMetadata GetAccesor()\r\n{\r\n\treturn __accesor;\r\n}");           
            }

            clsWriter.AppendLine().Append("}").AppendLine();

            if (_option.GenerateAccesor)
            {
                clsWriter.AppendLine(2);
                GenerateAccesor(info, clsWriter);
            }

            writer.Save(info.Name);

        }

        private void GenerateAccesor(MessageClassInfo info, CodeWriter clsWriter)
        {
            clsWriter.Append($"public sealed class {info.Name}Accesor : IReflectorMetadata").AppendLine();
            clsWriter.Append("{")
                .AppendLine().Append('\t', 1);

            var body = clsWriter.Block("ACCESOR_BODY");
            var fields = info.Fields.Values.OrderBy(x=>x.Field.Number);

            var getTypeSwtich = new StringBuilder();
            var getValueSwtich = new StringBuilder();
            var setValueSwtich = new StringBuilder();
            var getPropertyName = new StringBuilder();
            var getPropertyCode = new StringBuilder();

            foreach (var field in fields)
            {
                body.Append($"public const int {field.Name} = {field.Field.Number};").AppendLine();

                var nullable = field.Field.Option.Optional && field.Field.Type.TypeDeclaration.Nullable ? "?" : "";

                getTypeSwtich.Append($"{field.Name} => typeof({field.Type}{nullable}),\r\n");
                getValueSwtich.Append($"{field.Name} => obj.{field.Name},\r\n");
                setValueSwtich.Append($"case {field.Name}:  obj.{field.Name} = ({field.Type}{nullable})value;break;\r\n");
                getPropertyName.Append($"{field.Name} => \"{field.Name}\",\r\n");
                getPropertyCode.Append($"\"{field.Name}\" => {field.Name},\r\n");
            }

            body.Append("private readonly int[] _props = new []").AppendLine();
            body.Append("{").AppendLine();

            body.Append('\t',1).Append(string.Join(",", fields.Select(x => x.Name)));
            body.AppendLine();

            body.Append("};").AppendLine(2);

            body.Append("public int[] GetPropertyCodes() => _props;")
                .AppendLine();

            body.AppendTemplate(Template, new Dictionary<string, object>
            {
                ["TYPE"] = info.Name,
                ["SWITCH"]= getTypeSwtich.ToString(),
                ["GET_VALUE"] = getValueSwtich.ToString(),
                ["SET_VALUE"] = setValueSwtich.ToString(),
                ["GET_PROPERTY_NAME"] = getPropertyName.ToString(),
                ["GET_PROPERTY_CODE"] = getPropertyCode.ToString()
            });

            clsWriter.AppendLine();
            clsWriter.Append("}")
                .AppendLine();
        }

        string Template = @"
public string GetPropertyName(int propertyCode)
{
    return propertyCode switch
    {
       @{GET_PROPERTY_NAME}
        _ => throw new InvalidOperationException(""property code not supported""),
    };
}

public int GetPropertyCode(string propertyName)
{
    return propertyName switch
    {
        @{GET_PROPERTY_CODE}
        _ => -1,
    };
}

public Type GetPropertyType(int propertyCode)
{
    return propertyCode switch
    {
        @{SWITCH}
        _ => throw new InvalidOperationException(""property code not supported""),
    };
}
       
public object GetValue(object target, int propertyCode)
{
    @{TYPE} obj = (@{TYPE})target;
    return propertyCode switch
    {
        @{GET_VALUE}
        _ => throw new InvalidOperationException(""property code not supported""),
    };
}

public void SetValue(object target, int propertyCode, object value)
{
    @{TYPE} obj = (@{TYPE})target;
    switch (propertyCode)
    {
        @{SET_VALUE}
        default: throw new InvalidOperationException(""property code not supported"");
    }
}
";

    }

   

    
}