using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;

namespace CKL.Libs.SiteBuilder.Rendering;

// Replaces the default CodeBlockRenderer.
// Fenced blocks tagged "mermaid" are emitted as <div class="mermaid"> so that
// the vendored mermaid.js can pick them up client-side.
// All other code blocks are delegated to the original renderer unchanged.
internal sealed class MermaidCodeBlockRenderer : HtmlObjectRenderer<CodeBlock>
{
    private readonly CodeBlockRenderer _default;

    public MermaidCodeBlockRenderer(CodeBlockRenderer defaultRenderer)
        => _default = defaultRenderer;

    protected override void Write(HtmlRenderer renderer, CodeBlock obj)
    {
        if (obj is FencedCodeBlock { Info: "mermaid" })
        {
            renderer.WriteLine("<div class=\"mermaid\">");
            renderer.WriteLeafRawLines(obj, writeEndOfLines: true, escape: true);
            renderer.WriteLine("</div>");
        }
        else
        {
            _default.Write(renderer, obj);
        }
    }
}
