using Microsoft.Iris.Data;
using Microsoft.Iris.Markup;
using Microsoft.Iris.Session;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Microsoft.Iris.Asm;

[Serializable]
internal class AsmMarkupLoadResult : MarkupLoadResult
{
    private MarkupConstantsTable _constantsTable;
    private MarkupImportTables _importTables;
    private Resource _resource;
    private AsmMarkupLoader _loader;
    private bool _doneWithLoader;

    public AsmMarkupLoadResult(Resource resource, string uri)
      : base(uri)
    {
        _resource = resource;
        if (uri != resource.Uri)
            _uriUnderlying = resource.Uri;
        _loader = new AsmMarkupLoader(this, resource);
    }

    public AsmMarkupLoadResult(string uri)
      : base(uri)
    {
    }

    protected AsmMarkupLoadResult(SerializationInfo info, StreamingContext context) : base(info, context)
    {
        _constantsTable = info.GetValue<MarkupConstantsTable>(nameof(ConstantsTable));
        _importTables = info.GetValue<MarkupImportTables>(nameof(ImportTables));
    }

    public override bool IsSource => true;

    internal AsmMarkupLoader Loader => _loader;

    public override MarkupConstantsTable ConstantsTable => _constantsTable;

    public override MarkupImportTables ImportTables => _importTables;

    public override void Load(LoadPass currentPass)
    {
        if (_doneWithLoader)
            return;
        ErrorManager.EnterContext(ErrorContextUri);
        _loader.Validate(currentPass);
        ErrorManager.ExitContext();
        if (currentPass != LoadPass.Done)
            return;
        if (!MarkupSystem.TrackAdditionalMetadata)
            _loader = null;
        _doneWithLoader = true;
    }

    public void ValidateType(MarkupTypeSchema typeSchema, LoadPass currentPass)
    {
        var loadData = typeSchema.LoadData;
        if (loadData == null)
            return;
        ErrorManager.EnterContext(ErrorContextUri);
        // TODO: loadData.Validate(currentPass);
        ErrorManager.ExitContext();
    }

    public void ValidationComplete()
    {
        if (_resource != null)
        {
            _resource.Free();
            _resource = null;
        }
        if (Status == LoadResultStatus.Loading)
            SetStatus(LoadResultStatus.Success);
        foreach (MarkupTypeSchema markupTypeSchema in ExportTable)
            markupTypeSchema.Seal();
    }

    public void SetConstantsTable(MarkupConstantsTable constantsTable) => _constantsTable = constantsTable;

    public void SetImportTables(MarkupImportTables importTables) => _importTables = importTables;

    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);

        info.AddValue(nameof(_resource), _resource);
    }
}
