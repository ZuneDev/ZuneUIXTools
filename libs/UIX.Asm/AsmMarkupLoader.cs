﻿using Microsoft.Iris.Asm.Models;
using Microsoft.Iris.Data;
using Microsoft.Iris.Markup;
using Microsoft.Iris.Session;
using Sprache;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.Iris.Asm;

internal class AsmMarkupLoader
{
    private readonly string _asmSource;
    private readonly AsmMarkupLoadResult _loadResult;
    private LoadPass _currentValidationPass;
    private bool _usingSharedBinaryDataTable;
    private AsmMarkupLoadResult _binaryDataTableLoadResult;
    private MarkupBinaryDataTable _binaryDataTable;
    private ObjectSection _objectSection;

    private readonly Dictionary<string, LoadResult> _importedNamespaces = new();
    private readonly Dictionary<string, ushort> _constants = new();
    private readonly HashSet<string> _referencedNamespaces = new();

    internal unsafe AsmMarkupLoader(AsmMarkupLoadResult loadResult, Resource resource)
    {
        _loadResult = loadResult;
        if (resource.Status != ResourceStatus.Available)
            throw new InvalidOperationException("Resource must be available for reading");

        var data = (byte*)resource.Buffer;

        // Check for BOM (byte order mark)
        int sourceStartOffset = 0;
        if (resource.Length > 3 && (*(uint*)data & 0x00FFFFFF) == 0x00BFBBEF)
            sourceStartOffset = 3;
        _asmSource = Encoding.UTF8.GetString(data + sourceStartOffset, (int)(resource.Length - sourceStartOffset));
    }

    public bool HasErrors { get; protected set; }

    public Program Program { get; private set; }

    public LoadResult FindDependency(string prefix)
    {
        if (prefix == null)
            return MarkupSystem.UIXGlobal;
        if (prefix == "me")
            return _loadResult;
        
        return _importedNamespaces[prefix];
    }

    public TypeSchema ResolveTypeFromQualifiedName(QualifiedTypeName qualifiedName)
    {
        if (qualifiedName == null)
            throw new ArgumentNullException(nameof(qualifiedName));

        var typeName = qualifiedName.TypeName;
        var result = FindDependency(qualifiedName.NamespacePrefix);

        if (result.Status == LoadResultStatus.Error)
        {
            ErrorManager.ReportError(qualifiedName.Line, qualifiedName.Column,
                $"Type '{typeName}' could not be resolved because {result.Uri} failed to load");
            MarkHasErrors();
            return null;
        }

        var type = result.FindType(typeName);

        if (type is null)
        {
            ErrorManager.ReportError(qualifiedName.Line, qualifiedName.Column,
                $"No type with the name '{typeName}' was exported from {result.Uri}");
            MarkHasErrors();
        }

        return type;
    }

    public void MarkHasErrors()
    {
        if (HasErrors)
            return;

        HasErrors = true;
        _loadResult.MarkLoadFailed();
    }

    public void Validate(LoadPass currentPass)
    {
        if (_currentValidationPass >= currentPass)
            return;
        _currentValidationPass = currentPass;

        if (_asmSource == null)
            return;

        if (Program is null && !HasErrors)
        {
            var parseResult = Lexer.Program.TryParse(_asmSource);
            if (parseResult.WasSuccessful)
            {
                Program = parseResult.Value;
                _objectSection = new(Program, _loadResult);
            }
            else
            {
                foreach (var errors in parseResult.Expectations)
                    ReportError(errors, parseResult.Remainder.Line, parseResult.Remainder.Column);
                return;
            }

            _usingSharedBinaryDataTable = Program.DataTableDirective is not null;
            if (_usingSharedBinaryDataTable)
            {   
                // Import the shared data table
                var dataTableUri = Program.DataTableDirective.Uri;
                var dataTableLoadResult = MarkupSystem.ResolveLoadResult(dataTableUri, _loadResult.IslandReferences);
                if (dataTableLoadResult is null)
                {
                    ReportError($"Unable to load '{dataTableUri}' (shared data table)", Program.DataTableDirective);
                    return;
                }
                
                if (dataTableLoadResult is not AsmMarkupLoadResult)
                {
                    ReportError($"Shared data table at '{dataTableUri}' was not UIXA", Program.DataTableDirective);
                    return;
                }

                _binaryDataTableLoadResult = (AsmMarkupLoadResult)dataTableLoadResult;
                if (_binaryDataTableLoadResult != null)
                {
                    _binaryDataTableLoadResult.Load(_currentValidationPass);
                    _binaryDataTable = _binaryDataTableLoadResult.BinaryDataTable;

                    _binaryDataTable.SharedDependenciesTableWithBinaryDataTable ??= [_binaryDataTableLoadResult];
                    _loadResult.SetDependenciesTable(_binaryDataTable.SharedDependenciesTableWithBinaryDataTable);
                }
                else
                {
                    MarkHasErrors();
                }
            }
            else
            {
                _binaryDataTable = new MarkupBinaryDataTable(_loadResult.Uri);
                _binaryDataTable.SetConstantsTable(new MarkupConstantsTable());
                _binaryDataTable.SetSourceMarkupImportTables(new SourceMarkupImportTables());
            }
            
            _loadResult.SetBinaryDataTable(_binaryDataTable);
        }

        if (_currentValidationPass == LoadPass.DeclareTypes)
        {
            foreach (var nsImport in Program.Directives.OfType<NamespaceImport>())
            {
                LoadResult loadResult;
                if (nsImport.Uri == "Me")
                    loadResult = _loadResult;
                else
                    loadResult = MarkupSystem.ResolveLoadResult(nsImport.Uri, _loadResult.IslandReferences);

                if (loadResult == null || loadResult is ErrorLoadResult)
                    ReportError($"Unable to load '{nsImport.Uri}' (xmlns prefix '{nsImport.Name}')", nsImport);
                else if (loadResult.Status == LoadResultStatus.Error)
                    MarkHasErrors();
                if (MarkupSystem.CompileMode)
                    loadResult.SetCompilerReferenceName(nsImport.Uri);

                if (loadResult != null)
                {
                    _importedNamespaces[nsImport.Name] = loadResult;
                    TrackImportedLoadResult(loadResult);
                }
            }
        }

        foreach (LoadResult loadResult in _importedNamespaces.Values)
        {
            if (loadResult != _loadResult && loadResult != MarkupSystem.UIXGlobal)
            {
                loadResult.Load(_currentValidationPass);
                if (loadResult.Status == LoadResultStatus.Error)
                    MarkHasErrors();
            }
        }

        if (Program != null && _currentValidationPass == LoadPass.Full)
        {
            TrackImports();
        }

        if (_currentValidationPass == LoadPass.DeclareTypes)
        {
            _loadResult.SetExportTable(PrepareExportTable());
            //_loadResult.SetAliasTable(PrepareAliasTable());
        }
        else if (_currentValidationPass == LoadPass.PopulatePublicModel)
        {
            if (Program == null)
                return;

            //foreach (ValidateClass validateClass in _parseResult.ClassList)
            //    validateClass.TypeExport?.BuildProperties();
        }
        else if (_currentValidationPass != LoadPass.Full)
        {
            if (HasErrors)
                _loadResult.MarkLoadFailed();

            MarkupImportTables importTables = null;
            if (_binaryDataTable.SourceMarkupImportTables != null)
            {
                importTables = _binaryDataTable.SourceMarkupImportTables.PrepareImportTables();
                _loadResult.SetImportTables(importTables);
            }

            MarkupLineNumberTable lineNumberTable = new();
            MarkupConstantsTable constantsTable = _loadResult.BinaryDataTable?.ConstantsTable ?? new();

            _loadResult.SetLineNumberTable(lineNumberTable);
            _loadResult.SetDataMappingsTable(PrepareDataMappingTable());
            _loadResult.ValidationComplete();

            ByteCodeReader reader = null;
            if (!HasErrors)
            {
                // Add constants
                var stringTypeSchema = UIXTypes.MapIDToType(UIXTypeID.String);

                foreach (var constant in Program.Body.OfType<ConstantDirective>())
                {
                    object constantValue, persistData = null;
                    MarkupConstantPersistMode mode;

                    var constantTypeSchema = ResolveTypeFromQualifiedName(constant.TypeName);

                    if (constant is StringEncodedConstantDirective stringEncodedConstant)
                    {
                        var stringParseResult = constantTypeSchema.TypeConverter(stringEncodedConstant.Content, stringTypeSchema, out constantValue);
                        if (stringParseResult.Failed)
                        {
                            ReportError($"Failed to create an instance of '{constant.TypeName}' from '{stringEncodedConstant.Content}'", constant);
                            continue;
                        }

                        persistData = stringEncodedConstant.Content;
                        mode = MarkupConstantPersistMode.FromString;
                    }
                    else if (constant is CanonicalInstanceConstantDirective canonicalInstanceConstant)
                    {
                        var canonicalName = canonicalInstanceConstant.CanonicalName;
                        
                        persistData = canonicalName;
                        mode = MarkupConstantPersistMode.Canonical;

                        constantValue = constantTypeSchema.FindCanonicalInstance(canonicalName);

                        if (constantValue is null)
                        {
                            var canonicalParseResult = constantTypeSchema.TypeConverter(canonicalName, stringTypeSchema, out constantValue);
                            if (canonicalParseResult.Failed)
                            {
                                ReportError($"Failed to get canonical instance '{canonicalName}' from '{constant.TypeName}'", constant);
                                continue;
                            }
                        }
                    }
                    else if (constant is BinaryEncodedConstantDirective binaryEncodedConstant)
                    {
                        if (!constantTypeSchema.SupportsBinaryEncoding)
                        {
                            ReportError($"Constant was binary-encoded, but {constant.TypeName} does not support binary encoding.", constant);
                            continue;
                        }

                        var contentBuffer = binaryEncodedConstant.Content;

                        ByteCodeWriter binaryConstantWriter = new();
                        binaryConstantWriter.Write(contentBuffer, (uint)contentBuffer.Length);

                        var binaryConstantReader = binaryConstantWriter.CreateReader();
                        constantValue = constantTypeSchema.DecodeBinary(binaryConstantReader);

                        persistData = constantValue;
                        mode = MarkupConstantPersistMode.Binary;
                    }
                    else
                    {
                        ReportError($"Constant of type '{constant.TypeName}' was not encoded in a recognized format.", constant);
                        continue;
                    }

                    var constantIndex = (ushort)constantsTable.Add(constantTypeSchema, constantValue, mode, persistData);
                    _constants.Add(constant.Name, constantIndex);
                }

                _objectSection.Constants = _constants;
                reader = _objectSection.Encode();
                UpdateExportOffsets();
            }

            if (!_usingSharedBinaryDataTable)
            {
                constantsTable.PrepareForRuntimeUse();
                _loadResult.SetConstantsTable(constantsTable);
            }

            lineNumberTable.PrepareForRuntimeUse();

            if (reader != null)
                _loadResult.SetObjectSection(reader);
            
            _loadResult.SetDependenciesTable(PrepareDependenciesTable());

            if (!MarkupSystem.TrackAdditionalMetadata)
                Program = null;
        }
    }

    private void TrackImports()
    {
        foreach (var import in Program.Imports)
        {
            if (import is TypeImport typeImport)
            {
                var typeSchema = ResolveTypeFromQualifiedName(typeImport.QualifiedName);
                TrackImportedType(typeSchema);
            }
            else if (import is NamespaceImport nsImport)
            {
                if (!_referencedNamespaces.Contains(nsImport.Name))
                    ErrorManager.ReportWarning(nsImport.Line, nsImport.Column, $"Unreferenced namespace '{nsImport.Name}'");
            }
            else if (import is ConstructorImport ctorImport)
            {
                var typeSchema = ResolveTypeFromQualifiedName(ctorImport.QualifiedName);
                var ctorParamTypes = ctorImport.ParameterTypes.Select(ResolveTypeFromQualifiedName).ToArray();

                var ctorSchema = typeSchema.FindConstructor(ctorParamTypes);

                TrackImportedConstructor(ctorSchema);
            }
            else if (import is MethodImport mthdImport)
            {
                var typeSchema = ResolveTypeFromQualifiedName(mthdImport.QualifiedName);
                var mthdParamTypes = mthdImport.ParameterTypes.Select(ResolveTypeFromQualifiedName).ToArray();

                var mthdSchema = typeSchema.FindMethod(mthdImport.MethodName, mthdParamTypes);

                TrackImportedMethod(mthdSchema);
            }
            else if (import is NamedMemberImport mbrsImport)
            {
                var typeSchema = ResolveTypeFromQualifiedName(mbrsImport.QualifiedName);

                foreach (var memberName in mbrsImport.MemberNames)
                {
                    var propMember = typeSchema.FindProperty(memberName);
                    if (propMember is not null)
                    {
                        TrackImportedProperty(propMember);
                        continue;
                    }

                    var eventMember = typeSchema.FindEvent(memberName);
                    if (eventMember is not null)
                    {
                        TrackImportedEvent(eventMember);
                        continue;
                    }
                }
            }
        }
    }

    private TypeSchema[] PrepareExportTable()
    {
        var exportDirectives = Program.Directives.OfType<ExportDirective>().ToArray();
        var exports = new TypeSchema[exportDirectives.Length];

        for (int i = 0; i < exportDirectives.Length; i++)
        {
            ExportDirective exportDirective = exportDirectives[i];

            var markupType = (MarkupType)Enum.Parse(typeof(MarkupType), exportDirective.BaseTypeName);
            var exportedTypeSchema = MarkupTypeSchema.Build(markupType, _loadResult, exportDirective.LabelPrefix);

            exportedTypeSchema.SetListenerCount(exportDirective.ListenerCount);

            // The offsets aren't known until the object section has been encoded,
            // so we have to defer setting them until the Full load pass.

            exports[i] = exportedTypeSchema;
        }

        return exports;
    }

    private LoadResult[] PrepareDependenciesTable()
    {
        LoadResult[] loadResultArray = LoadResult.EmptyList;
        int length = 0;
        if (_binaryDataTable.SourceMarkupImportTables != null)
        {
            foreach (LoadResult importedLoadResult in _binaryDataTable.SourceMarkupImportTables.ImportedLoadResults)
            {
                if (importedLoadResult != _loadResult)
                    ++length;
            }
        }
        if (length != 0)
        {
            loadResultArray = new LoadResult[length];
            int index = 0;
            foreach (LoadResult importedLoadResult in _binaryDataTable.SourceMarkupImportTables.ImportedLoadResults)
            {
                if (importedLoadResult != _loadResult)
                {
                    loadResultArray[index] = importedLoadResult;
                    ++index;
                }
            }
        }
        return loadResultArray;
    }

    private MarkupDataMapping[] PrepareDataMappingTable()
    {
        // TODO
        return [];
    }

    private void UpdateExportOffsets()
    {
        var exportDirectives = Program.Directives.OfType<ExportDirective>().ToArray();

        uint[] GetOffsets(string prefix) => _objectSection.LabelOffsetMap
            .Where(kvp => kvp.Key.StartsWith(prefix))
            .Select(kvp => kvp.Value)
            .ToArray();

        for (int i = 0; i < _loadResult.ExportTable.Length; i++)
        {
            var exportedTypeSchema = (MarkupTypeSchema)_loadResult.ExportTable[i];
            var exportDirective = exportDirectives[i];

            // Set all offsets
            if (_objectSection.LabelOffsetMap.TryGetValue(exportDirective.InitializePropertiesLabel, out var propOffset))
                exportedTypeSchema.SetInitializePropertiesOffset(propOffset);

            if (_objectSection.LabelOffsetMap.TryGetValue(exportDirective.InitializeContentLabel, out var contOffset))
                exportedTypeSchema.SetInitializeContentOffset(contOffset);

            if (_objectSection.LabelOffsetMap.TryGetValue(exportDirective.InitializeLocalsInputLabel, out var loclOffset))
                exportedTypeSchema.SetInitializeLocalsInputOffset(loclOffset);

            var evaliOffsets = GetOffsets(exportDirective.InitialEvaluateOffsetsLabelPrefix);
            exportedTypeSchema.SetInitialEvaluateOffsets(evaliOffsets);

            var evalfOffsets = GetOffsets(exportDirective.FinalEvaluateOffsetsLabelPrefix);
            exportedTypeSchema.SetFinalEvaluateOffsets(evalfOffsets);

            var rfshOffsets = GetOffsets(exportDirective.FinalEvaluateOffsetsLabelPrefix);
            exportedTypeSchema.SetRefreshListenerGroupOffsets(rfshOffsets);
        }
    }

    public void ReportError(string error, int line, int column)
    {
        MarkHasErrors();
        ErrorManager.ReportError(line, column, error);
    }

    public void ReportError(string error, IAsmItem item) => ReportError(error, item.Line, item.Column);

    public void TrackImportedLoadResult(LoadResult loadResult)
    {
        if (loadResult == MarkupSystem.UIXGlobal)
            return;
        
        for (int index = 0; index < _binaryDataTable.SourceMarkupImportTables.ImportedLoadResults.Count; ++index)
        {
            if ((LoadResult)_binaryDataTable.SourceMarkupImportTables.ImportedLoadResults[index] == loadResult)
                return;
        }

        _binaryDataTable.SourceMarkupImportTables.ImportedLoadResults.Add(loadResult);
    }

    public int TrackImportedType(TypeSchema type)
    {
        if (type is null) return -1;

        TrackImportedLoadResult(type.Owner);
        return TrackImportedSchema(_binaryDataTable.SourceMarkupImportTables.ImportedTypes, type);
    }

    public int TrackImportedConstructor(ConstructorSchema constructor)
    {
        if (constructor is null) return -1;

        int num = TrackImportedSchema(_binaryDataTable.SourceMarkupImportTables.ImportedConstructors, constructor);
        TrackImportedType(constructor.Owner);
        foreach (TypeSchema parameterType in constructor.ParameterTypes)
            TrackImportedType(parameterType);
        return num;
    }

    public int TrackImportedProperty(PropertySchema property)
    {
        if (property is null) return -1;

        int num = TrackImportedSchema(_binaryDataTable.SourceMarkupImportTables.ImportedProperties, property);
        TrackImportedType(property.Owner);
        return num;
    }

    public int TrackImportedMethod(MethodSchema method)
    {
        if (method is null) return -1;

        int num = TrackImportedSchema(_binaryDataTable.SourceMarkupImportTables.ImportedMethods, method);
        TrackImportedType(method.Owner);
        foreach (TypeSchema parameterType in method.ParameterTypes)
            TrackImportedType(parameterType);
        return num;
    }

    public int TrackImportedEvent(EventSchema evt)
    {
        if (evt is null) return -1;

        int num = TrackImportedSchema(_binaryDataTable.SourceMarkupImportTables.ImportedEvents, evt);
        TrackImportedType(evt.Owner);
        return num;
    }

    public int TrackImportedSchema(Vector importList, object schema)
    {
        if (schema is null)
            return -1;

        for (int index = 0; index < importList.Count; ++index)
            if (importList[index] == schema)
                return index;
        
        int count = importList.Count;
        importList.Add(schema);
        return count;
    }
}