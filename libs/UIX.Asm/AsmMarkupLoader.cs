using Microsoft.Iris.Asm.Models;
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
    private string _asmSource;
    private Program _program;
    private LoadPass _currentValidationPass;
    private readonly AsmMarkupLoadResult _loadResult;
    private bool _usingSharedBinaryDataTable;
    private SourceMarkupImportTables _importTables;
    private ObjectSection _objectSection;

    private readonly Dictionary<string, LoadResult> _importedNamespaces = new();
    private readonly HashSet<string> _referencedNamespaces = new();

    protected AsmMarkupLoader(AsmMarkupLoadResult loadResult)
    {
        _loadResult = loadResult;
        _objectSection = new(_program, _loadResult);
    }

    public bool HasErrors { get; protected set; }

    internal static unsafe AsmMarkupLoader Load(AsmMarkupLoadResult loadResult, Resource resource)
    {
        AsmMarkupLoader owner = new(loadResult);

        if (resource.Status != ResourceStatus.Available)
            throw new InvalidOperationException("Resource must be available for reading");

        var data = (byte*)resource.Buffer;

        // Check for BOM (byte order mark)
        int sourceStartOffset = 0;
        if (resource.Length >= 4 && *data == 0xEF && *(data + 1) == 0xBB && *(data + 2) == 0xBF)
            sourceStartOffset = 3;

        owner._asmSource = Encoding.UTF8.GetString(data + sourceStartOffset, (int)(resource.Length - sourceStartOffset));

        var parseResult = Lexer.Program.TryParse(owner._asmSource);
        if (parseResult.WasSuccessful)
            owner._program = parseResult.Value;
        else
            owner.MarkHasErrors();

        return owner;
    }

    public LoadResult FindDependency(string prefix)
    {
        if (prefix == null)
            return MarkupSystem.UIXGlobal;
        
        return _importedNamespaces[prefix];
    }

    public TypeSchema ResolveTypeFromQualifiedName(string qualifiedName)
    {
        if (qualifiedName == null)
            throw new ArgumentNullException(nameof(qualifiedName));

        string typeName;
        LoadResult result;

        int idx = qualifiedName.IndexOf(':');
        if (idx <= 0)
        {
            typeName = qualifiedName[..idx];
            result = FindDependency(qualifiedName[..idx]);
        }
        else
        {
            typeName = qualifiedName;
            result = _loadResult;
        }

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
                _program = parseResult.Value;
            else
                foreach (var errors in parseResult.Expectations)
                    ReportError(errors, -1, -1);

            if (_loadResult.BinaryDataTable != null)
            {
                _importTables = _loadResult.BinaryDataTable.SourceMarkupImportTables;
                _usingSharedBinaryDataTable = true;
            }
            else
            {
                _importTables = new SourceMarkupImportTables();
            }

            foreach (var nsImport in _program.Directives.OfType<NamespaceImport>())
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

        if (_program != null && currentPass != LoadPass.Done)
        {
            //foreach (ValidateClass validateClass in _program.ClassList)
            //    validateClass.Validate(_currentValidationPass);

            //foreach (ValidateDataMapping dataMapping in _program.DataMappingList)
            //    dataMapping.Validate(_currentValidationPass);

            //foreach (ValidateAlias alias in _program.AliasList)
            //    alias.Validate(_currentValidationPass);

            if (_currentValidationPass == LoadPass.Full)
            {
                foreach (var nsImport in _program.Directives.OfType<NamespaceImport>())
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
            if (_program == null)
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
                reader = _objectSection.Encode();

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
                _program = null;

            //foreach (DisposableObject validateObject in _validateObjects)
            //    validateObject.Dispose(this);
        }
    }

    private TypeSchema[] PrepareExportTable()
    {
        var exportDirectives = _program.Directives.OfType<ExportDirective>().ToArray();
        var exports = new TypeSchema[exportDirectives.Length];

        for (int i = 0; i < exportDirectives.Length; i++)
        {
            ExportDirective exportDirective = exportDirectives[i];

            var markupType = (MarkupType)Enum.Parse(typeof(MarkupType), exportDirective.BaseTypeName);
            var exportedTypeSchema = MarkupTypeSchema.Build(markupType, _loadResult, exportDirective.LabelPrefix);

            // Set all offsets
            var propOffset = _objectSection.LabelOffsetMap[exportDirective.InitializePropertiesLabel];
            exportedTypeSchema.SetInitializePropertiesOffset(propOffset);

            var contOffset = _objectSection.LabelOffsetMap[exportDirective.InitializeContentLabel];
            exportedTypeSchema.SetInitializeContentOffset(contOffset);

            var loclOffset = _objectSection.LabelOffsetMap[exportDirective.InitializeLocalsInputLabel];
            exportedTypeSchema.SetInitializeLocalsInputOffset(loclOffset);

            var evaliOffsets = _objectSection.LabelOffsetMap
                .Where(kvp => kvp.Key.StartsWith(exportDirective.InitialEvaluateOffsetsLabelPrefix))
                .Select(kvp => kvp.Value)
                .ToArray();
            exportedTypeSchema.SetInitialEvaluateOffsets(evaliOffsets);

            var evalfOffsets = _objectSection.LabelOffsetMap
                .Where(kvp => kvp.Key.StartsWith(exportDirective.FinalEvaluateOffsetsLabelPrefix))
                .Select(kvp => kvp.Value)
                .ToArray();
            exportedTypeSchema.SetFinalEvaluateOffsets(evalfOffsets);

            var rfshOffsets = _objectSection.LabelOffsetMap
                .Where(kvp => kvp.Key.StartsWith(exportDirective.RefreshGroupOffsetsLabelPrefix))
                .Select(kvp => kvp.Value)
                .ToArray();
            exportedTypeSchema.SetRefreshListenerGroupOffsets(rfshOffsets);

            exportedTypeSchema.SetListenerCount(exportDirective.ListenerCount);

            exports[i] = exportedTypeSchema;
        }

        return exports;
    }
    private LoadResult[] PrepareDependenciesTable()
    {
        // TODO
        return [];
    }

    private MarkupDataMapping[] PrepareDataMappingTable()
    {
        // TODO
        return [];
    }

    public void ReportError(string error, int line, int column)
    {
        MarkHasErrors();
        ErrorManager.ReportError(line, column, error);
    }

    public void ReportError(string error, IAsmItem item)
        => ReportError(error, item.Line, item.Column);

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