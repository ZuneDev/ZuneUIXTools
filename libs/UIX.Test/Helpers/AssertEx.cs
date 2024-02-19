using Microsoft.Iris.Markup;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace UIX.Test.Helpers;

internal static class AssertEx
{
    public static void DeepEquivalent(LoadResult expected, LoadResult actual)
    {
        if (actual is MarkupLoadResult markupAc)
        {
            Assert.IsAssignableFrom<MarkupLoadResult>(expected);
            var markupEx = (MarkupLoadResult)expected;

            Assert.Equal(markupEx.AliasTable, markupAc.AliasTable);
            Collection(markupEx.ExportTable, markupAc.ExportTable, DeepEquivalent);
            Equivalent(markupEx.BinaryDataTable, markupAc.BinaryDataTable);
        }
    }

    public static void DeepEquivalent(TypeSchema expected, TypeSchema actual)
    {
        if (BothNull(expected, actual))
            return;

        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.AlternateName, actual.AlternateName);
        WeakEquivalent(expected.Base, actual.Base);
        Assert.Equal(expected.IsEnum, actual.IsEnum);
        Assert.Equal(expected.IsStatic, actual.IsStatic);
        Assert.Equal(expected.SupportsBinaryEncoding, actual.SupportsBinaryEncoding);
        
        Collection(expected.Constructors, actual.Constructors, (ex, ac) => Assert.Equivalent(ex, ac));
    }

    public static void Equivalent(MarkupBinaryDataTable expected, MarkupBinaryDataTable actual)
    {
        if (BothNull(expected, actual))
            return;

        HashableCollection(expected.Strings, actual.Strings);
        Equivalent(expected.ImportTables, actual.ImportTables);
        Assert.Equivalent(expected.ConstantsTable, actual.ConstantsTable);
    }

    public static void Equivalent(MarkupImportTables expected, MarkupImportTables actual)
    {
        Collection(expected.TypeImports, actual.TypeImports, WeakEquivalent);
        Collection(expected.EventImports, actual.EventImports, (ex, ac) => Assert.Equivalent(ex, ac));
        Collection(expected.ConstructorImports, actual.ConstructorImports, (ex, ac) => Assert.Equivalent(ex, ac));
        Collection(expected.MethodImports, actual.MethodImports, (ex, ac) => Assert.Equivalent(ex, ac));
        Collection(expected.PropertyImports, actual.PropertyImports, (ex, ac) => Assert.Equivalent(ex, ac));
    }

    public static void WeakEquivalent(LoadResult expected, LoadResult actual)
    {
        Assert.Equal(expected?.Uri, actual?.Uri);
    }

    public static void WeakEquivalent(TypeSchema expected, TypeSchema actual)
    {
        if (BothNull(expected, actual))
            return;

        Assert.Equal(expected.Name, actual.Name);
        Assert.Equivalent(expected.Owner, actual.Owner);
    }

    public static void Collection<T>(T[] expected, T[] actual, Action<T, T> assertion)
    {
        if (BothNull(expected, actual))
            return;

        Assert.Equal(expected.Length, actual.Length);
        Assert.All(actual, (ac, i) => assertion(expected[i], ac));
    }

    public static void Collection<T>(Vector<T> expected, Vector<T> actual, Action<T, T> assertion)
    {
        if (BothNull(expected, actual))
            return;

        Assert.Equal(expected.Count, actual.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            var ex = expected[i];
            var ac = actual[i];
            assertion(ex, ac);
        }
    }

    public static void HashableCollection<T>(Vector<T> expected, Vector<T> actual)
    {
        if (BothNull(expected, actual))
            return;

        var missing = expected.ToArray().ToHashSet();
        missing.SymmetricExceptWith(actual.ToArray());

        Assert.Empty(missing);
    }

    public static bool BothNull([NotNullWhen(true)] object expected, [NotNullWhen(true)] object actual)
    {
        if (expected is null)
        {
            Assert.Null(actual);
            return true;
        }

        return false;
    }
}
