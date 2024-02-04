using Microsoft.Iris.Data;
using Microsoft.Iris.Markup;
using Microsoft.Iris.Session;

namespace Microsoft.Iris.Asm;

public static class Assembler
{
    public static void RegisterLoader()
    {
        MarkupSystem.RegisterFactoryByExtension(".uixa", Load);
    }

    public static LoadResult Load(string uri)
    {
        Resource resource = ResourceManager.AcquireResource(uri);
        if (resource == null)
            return null;

        ErrorManager.EnterContext(resource.Uri);
        LoadResult loadResult = LoadResultCache.Read(resource.Uri);
        if (loadResult != null)
        {
            LoadResultCache.Write(uri, loadResult);
        }
        else
        {
            loadResult = new AsmMarkupLoadResult(resource, uri);
            if (loadResult != null)
                resource = null;
        }

        resource?.Free();
        ErrorManager.ExitContext();
        return loadResult;
    }
}
