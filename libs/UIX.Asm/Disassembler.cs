using Humanizer;
using Microsoft.Iris.Markup;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Iris.Asm;

public class Disassembler
{
    private readonly MarkupLoadResult _loadResult;

    private Disassembler(MarkupLoadResult loadResult)
    {
        _loadResult = loadResult;
    }

    public static Disassembler Load(MarkupLoadResult loadResult) => new(loadResult);

    public IEnumerable<IImport> GetImports()
    {
        foreach (var typeImport in _loadResult.ImportTables.TypeImports)
        {
            var uri = typeImport.Owner.Uri;

            // Skip default UIX namespace
            if (uri == "http://schemas.microsoft.com/2007/uix")
                continue;

            string name = uri;
            var schemeLength = uri.IndexOf("://");
            if (schemeLength > 0)
            {
                var scheme = uri[..schemeLength];
                if (scheme == "assembly")
                {
                    // Assume the URI represents a C# namespace
                    name = uri.Split('.', '/', '\\')[^1];
                }
                else
                {
                    // Assume the URI represents a file,
                    // skip the extension
                    name = uri.Split('.', '/', '\\')[^2];
                }
            }

            yield return new NamespaceImport(uri, name.Camelize());
        };
    }

    public IEnumerable<IBodyItem> GetBody()
    {
        var reader = _loadResult.ObjectSection;

        while (reader.CurrentOffset < reader.Size)
        {
            var opCode = (OpCode)reader.ReadByte();
            
            switch (opCode)
            {
                case OpCode.ConstructObject:
                    // COBJ <typeIndex>
                    yield return new Instruction("COBJ", [new(reader.ReadUInt16())]);
                    break;

                case OpCode.ConstructObjectIndirect:
                    // COBI <assignmentTypeIndex>
                    yield return new Instruction("COBI", [new(reader.ReadUInt16())]);
                    break;

                case OpCode.ConstructObjectParam:
                    // COBP <targetTypeIndex> <constructorIndex>
                    yield return new Instruction("COBP", [new(reader.ReadUInt16()), new(reader.ReadUInt16())]);
                    break;

                case OpCode.ConstructFromString:
                    // CSTR <typeIndex> <stringIndex>
                    yield return new Instruction("CSTR", [new(reader.ReadUInt16()), new(reader.ReadUInt16())]);
                    break;

                case OpCode.ConstructFromBinary:
                    // CBIN <typeIndex> <object:X>
                    var cbinTypeIndex = reader.ReadUInt16();
                    TypeSchema cbinTypeSchema = _loadResult.ImportTables.TypeImports[cbinTypeIndex];
                    object cbinObject = cbinTypeSchema.DecodeBinary(reader);

                    yield return new Instruction("CBIN", [new(cbinTypeIndex), new(cbinObject)]);
                    break;

                // TODO

                case OpCode.PropertyInitialize:
                    // PINI <propertyIndex>
                    yield return new Instruction("PINI", [new(reader.ReadUInt16())]);
                    break;

                case OpCode.PropertyInitializeIndirect:
                    // PINII <propertyIndex>
                    yield return new Instruction("PINII", [new(reader.ReadUInt16())]);
                    break;

                // TODO

                case OpCode.PushConstant:
                    // PSHC <constantIndex>
                    yield return new Instruction("PSHC", [new(reader.ReadUInt16())]);
                    break;

                // TODO

                case OpCode.ReturnValue:
                    // RET
                    yield return new Instruction("RET", []);
                    break;

                case OpCode.ReturnVoid:
                    // RETV
                    yield return new Instruction("RETV", []);
                    break;
            }
        }

        yield break;
    }

    public string Write()
    {
        _loadResult.Load(LoadPass.DeclareTypes);
        _loadResult.Load(LoadPass.PopulatePublicModel);
        _loadResult.Load(LoadPass.Full);
        _loadResult.Load(LoadPass.Done);

        List<IImport> imports = new(GetImports());
        List<IBodyItem> body = new(GetBody());

        Program asmProgram = new(imports, body);
        return asmProgram.ToString();
    }
}
