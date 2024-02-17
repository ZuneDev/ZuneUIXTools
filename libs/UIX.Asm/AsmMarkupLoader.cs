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
    private SourceMarkupImportTables _importTables;
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
        if (resource.Length >= 4 && *data == 0xEF && *(data + 1) == 0xBB && *(data + 2) == 0xBF)
            sourceStartOffset = 3;
        _asmSource = Encoding.UTF8.GetString(data + sourceStartOffset, (int)(resource.Length - sourceStartOffset));

        var parseResult = Lexer.Program.TryParse(_asmSource);
        if (parseResult.WasSuccessful)
            Program = parseResult.Value;
        else
            MarkHasErrors();
    }

    public bool HasErrors { get; protected set; }

    private Program Program { get; set; }

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

        return result.ExportTable.Where(e => e.Name == typeName).FirstOrDefault()
            ?? throw new Exception($"No type with the name '{typeName}' was exported from {result.Uri}");
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

        if (_currentValidationPass == LoadPass.DeclareTypes)
        {
            if (_asmSource == null)
                return;

            var parseResult = Lexer.Program.TryParse(_asmSource);
            if (parseResult.WasSuccessful)
            {
                Program = parseResult.Value;
                _objectSection = new(Program, _loadResult);
            }
            else
            {
                foreach (var errors in parseResult.Expectations)
                    ReportError(errors, -1, -1);
            }

            if (_loadResult.BinaryDataTable != null)
            {
                _importTables = _loadResult.BinaryDataTable.SourceMarkupImportTables;
                _usingSharedBinaryDataTable = true;
            }
            else
            {
                _importTables = new SourceMarkupImportTables();
            }

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

        if (Program != null && currentPass != LoadPass.Done)
        {
            //foreach (ValidateClass validateClass in _program.ClassList)
            //    validateClass.Validate(_currentValidationPass);

            //foreach (ValidateDataMapping dataMapping in _program.DataMappingList)
            //    dataMapping.Validate(_currentValidationPass);

            //foreach (ValidateAlias alias in _program.AliasList)
            //    alias.Validate(_currentValidationPass);

            if (_currentValidationPass == LoadPass.Full)
            {
                foreach (var typeImport in Program.Imports.OfType<TypeImport>())
                {
                    var typeSchema = ResolveTypeFromQualifiedName(typeImport.QualifiedName);
                    TrackImportedType(typeSchema);
                }

                foreach (var nsImport in Program.Imports.OfType<NamespaceImport>())
                {
                    if (!_referencedNamespaces.Contains(nsImport.Name))
                        ErrorManager.ReportWarning(nsImport.Line, nsImport.Column, $"Unreferenced namespace '{nsImport.Name}'");
                }
            }
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
        else
        {
            if (_currentValidationPass != LoadPass.Full)
                return;

            if (HasErrors)
                _loadResult.MarkLoadFailed();

            MarkupImportTables importTables = null;
            if (_importTables != null)
            {
                importTables = _importTables.PrepareImportTables();
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
                    var constantTypeSchema = ResolveTypeFromQualifiedName(constant.TypeName);

                    if (constant is EncodedConstantDirective encodedConstant)
                    {
                        var parseResult = constantTypeSchema.TypeConverter(encodedConstant.Content, stringTypeSchema, out constantValue);
                        if (parseResult.Failed)
                        {
                            ReportError($"Failed to create an instance of '{constant.TypeName}' from '{encodedConstant.Content}'", constant);
                            continue;
                        }

                        persistData = encodedConstant.Content;
                    }
                    else
                    {
                        var xmlElem = System.Xml.Linq.XElement.Parse(constant.Constructor);

                        constantValue = constantTypeSchema.ConstructDefault();

                        foreach (var attr in xmlElem.Attributes())
                        {
                            var propName = attr.Name.LocalName;
                            var prop = constantTypeSchema.FindProperty(propName);

                            var propConvertResult = prop.PropertyType.TypeConverter(attr.Value, stringTypeSchema, out var propValue);
                            if (propConvertResult.Failed)
                            {
                                ReportError($"Failed to set {constantTypeSchema.Name}.{propName}", constant);
                                continue;
                            }

                            prop.SetValue(ref constantValue, propValue);
                        }
                    }

                    MarkupConstantPersistMode mode;
                    if (constantTypeSchema.SupportsBinaryEncoding)
                    {
                        mode = MarkupConstantPersistMode.Binary;
                        persistData = constantValue;
                    }
                    else
                    {
                        mode = MarkupConstantPersistMode.FromString;
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

            //foreach (DisposableObject validateObject in _validateObjects)
            //    validateObject.Dispose(this);
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
        if (_importTables != null)
        {
            foreach (LoadResult importedLoadResult in _importTables.ImportedLoadResults)
            {
                if (importedLoadResult != _loadResult)
                    ++length;
            }
        }
        if (length != 0)
        {
            loadResultArray = new LoadResult[length];
            int index = 0;
            foreach (LoadResult importedLoadResult in _importTables.ImportedLoadResults)
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
        for (int index = 0; index < _importTables.ImportedLoadResults.Count; ++index)
        {
            if ((LoadResult)_importTables.ImportedLoadResults[index] == loadResult)
                return;
        }
        _importTables.ImportedLoadResults.Add(loadResult);
    }

    public int TrackImportedType(TypeSchema type)
    {
        TrackImportedLoadResult(type.Owner);
        return TrackImportedSchema(_importTables.ImportedTypes, type);
    }

    public int TrackImportedConstructor(ConstructorSchema constructor)
    {
        int num = TrackImportedSchema(_importTables.ImportedConstructors, constructor);
        TrackImportedType(constructor.Owner);
        foreach (TypeSchema parameterType in constructor.ParameterTypes)
            TrackImportedType(parameterType);
        return num;
    }

    public int TrackImportedProperty(PropertySchema property)
    {
        int num = TrackImportedSchema(_importTables.ImportedProperties, property);
        TrackImportedType(property.Owner);
        return num;
    }

    public int TrackImportedMethod(MethodSchema method)
    {
        int num = TrackImportedSchema(_importTables.ImportedMethods, method);
        TrackImportedType(method.Owner);
        foreach (TypeSchema parameterType in method.ParameterTypes)
            TrackImportedType(parameterType);
        return num;
    }

    public int TrackImportedEvent(EventSchema evt)
    {
        int num = TrackImportedSchema(_importTables.ImportedEvents, evt);
        TrackImportedType(evt.Owner);
        return num;
    }

    public int TrackImportedSchema(Vector importList, object schema)
    {
        for (int index = 0; index < importList.Count; ++index)
        {
            if (importList[index] == schema)
                return index;
        }
        int count = importList.Count;
        importList.Add(schema);
        return count;
    }
}