using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IrisLanguageServer;

public class DocumentContentRepository
{
    private readonly Dictionary<string, string> _entries = [];

    public bool AddOrUpdateContent(string uri, string content)
    {
        var exists = _entries.ContainsKey(uri);

        _entries[uri] = content;

        return exists;
    }

    public bool AddOrUpdateContent(DocumentUri uri, string content) => AddOrUpdateContent(uri.ToString(), content);

    public bool AddOrUpdateContent(TextDocumentItem item) => AddOrUpdateContent(item.Uri, item.Text);

    public bool DiscardDocument(string uri) => _entries.Remove(uri);

    public string GetContent(string uri) => _entries[uri];
}
