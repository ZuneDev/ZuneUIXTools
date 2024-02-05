using Microsoft.Iris.Asm.Models;
using Microsoft.Iris.Data;
using Microsoft.Iris.Library;
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

    private readonly Dictionary<string, LoadResult> _importedNamespaces = new();
    private readonly HashSet<string> _referencedNamespaces = new();

    protected AsmMarkupLoader(AsmMarkupLoadResult loadResult) => _loadResult = loadResult;

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

            foreach (var nsImport in _program.Imports.OfType<NamespaceImport>())
            {
                LoadResult loadResult;
                if (nsImport.Uri == "Me")
                    loadResult = _loadResult;
                else
                    loadResult = MarkupSystem.ResolveLoadResult(nsImport.Uri, _loadResult.IslandReferences);

                if (loadResult == null || loadResult is ErrorLoadResult)
                    ReportError($"Unable to load '{nsImport.Uri}' (xmlns prefix '{nsImport.Name}')", -1, -1);
                else if (loadResult.Status == LoadResultStatus.Error)
                    MarkHasErrors();
                if (MarkupSystem.CompileMode)
                    loadResult.SetCompilerReferenceName(nsImport.Uri);

                if (loadResult != null)
                {
                    _importedNamespaces[nsImport.Name] = loadResult;
                    //TrackImportedLoadResult(loadResult);
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
                foreach (var nsImport in _program.Imports.OfType<NamespaceImport>())
                {
                    if (!_referencedNamespaces.Contains(nsImport.Name))
                        ErrorManager.ReportWarning(nsImport.Line, nsImport.Column, $"Unreferenced namespace '{nsImport.Name}'");
                }
            }
        }

        if (_currentValidationPass == LoadPass.DeclareTypes)
        {
            //_loadResult.SetExportTable(PrepareExportTable());
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

            MarkupLineNumberTable lineNumberTable = new MarkupLineNumberTable();
            MarkupConstantsTable constantsTable = _loadResult.BinaryDataTable == null
                ? new MarkupConstantsTable()
                : _loadResult.BinaryDataTable.ConstantsTable;

            _loadResult.SetDataMappingsTable(PrepareDataMappingTable());
            _loadResult.ValidationComplete();

            ByteCodeReader reader = null;
            if (!HasErrors)
                EncodeOBJECTSection();
            //  reader = new MarkupEncoder(importTables, constantsTable, lineNumberTable).EncodeOBJECTSection(_parseResult, _loadResult.Uri, null);

            if (!_usingSharedBinaryDataTable)
            {
                constantsTable.PrepareForRuntimeUse();
                _loadResult.SetConstantsTable(constantsTable);
            }

            lineNumberTable.PrepareForRuntimeUse();
            _loadResult.SetLineNumberTable(lineNumberTable);

            if (reader != null)
                _loadResult.SetObjectSection(reader);
            
            _loadResult.SetDependenciesTable(PrepareDependenciesTable());

            if (!MarkupSystem.TrackAdditionalMetadata)
                _program = null;

            //foreach (DisposableObject validateObject in _validateObjects)
            //    validateObject.Dispose(this);
        }
    }

    private LoadResult[] PrepareDependenciesTable()
    {
        throw new NotImplementedException();
    }

    private MarkupDataMapping[] PrepareDataMappingTable()
    {
        throw new NotImplementedException();
    }

    public void ReportError(string error, int line, int column)
    {
        MarkHasErrors();
        ErrorManager.ReportError(line, column, error);
    }

    private void EncodeOBJECTSection()
    {
        var instructions = _program.Body.OfType<Instruction>();
    }
}