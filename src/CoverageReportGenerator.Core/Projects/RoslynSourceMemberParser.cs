using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using CoverageReportGenerator.Core.Utilities;

namespace CoverageReportGenerator.Core.Projects;

/// <summary>
/// C#ソースをRoslynで解析し、表示用メンバー情報を抽出する。
/// </summary>
public sealed class RoslynSourceMemberParser
{
    /// <summary>
    /// 指定ファイルからメソッド、プロパティ、ローカル関数、ラムダを取得する。
    /// </summary>
    public async Task<IReadOnlyList<SourceMember>> ParseFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!Path.GetExtension(filePath).Equals(".cs", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        var text = await SourceTextReader.ReadAllTextAsync(filePath, cancellationToken);
        var tree = CSharpSyntaxTree.ParseText(text, cancellationToken: cancellationToken);
        var root = await tree.GetRootAsync(cancellationToken);
        var normalizedPath = PathUtilities.NormalizeFullPath(filePath);
        var relative = Path.GetFileName(filePath);
        var members = new List<SourceMember>();

        // 宣言メンバー、アクセサ、ローカル関数、ラムダはSyntaxKindが分かれるため個別に集約する。
        foreach (var member in root.DescendantNodes().OfType<MemberDeclarationSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (member is MethodDeclarationSyntax method)
            {
                members.Add(CreateMember(normalizedPath, relative, method, SourceMemberKind.Method, GetContainingType(method), method.Identifier.Text, method.Identifier.Text, Signature(method)));
            }
            else if (member is ConstructorDeclarationSyntax constructor)
            {
                members.Add(CreateMember(normalizedPath, relative, constructor, SourceMemberKind.Constructor, GetContainingType(constructor), constructor.Identifier.Text, constructor.Identifier.Text, Signature(constructor)));
            }
            else if (member is PropertyDeclarationSyntax property)
            {
                members.Add(CreateMember(normalizedPath, relative, property, SourceMemberKind.Property, GetContainingType(property), property.Identifier.Text, property.Identifier.Text, property.Identifier.Text));
            }
        }

        foreach (var accessor in root.DescendantNodes().OfType<AccessorDeclarationSyntax>())
        {
            var property = accessor.FirstAncestorOrSelf<PropertyDeclarationSyntax>();
            if (property is null)
            {
                continue;
            }

            var accessorName = $"{property.Identifier.Text}.{accessor.Keyword.Text}";
            members.Add(CreateMember(normalizedPath, relative, accessor, SourceMemberKind.Accessor, GetContainingType(accessor), accessorName, accessorName, accessorName));
        }

        foreach (var localFunction in root.DescendantNodes().OfType<LocalFunctionStatementSyntax>())
        {
            members.Add(CreateMember(normalizedPath, relative, localFunction, SourceMemberKind.LocalFunction, GetContainingType(localFunction), localFunction.Identifier.Text, localFunction.Identifier.Text, Signature(localFunction)));
        }

        foreach (var lambda in root.DescendantNodes().Where(node => node is ParenthesizedLambdaExpressionSyntax or SimpleLambdaExpressionSyntax))
        {
            var parentName = FindParentMemberName(lambda);
            var display = $"lambda in {parentName}";
            members.Add(CreateMember(normalizedPath, relative, lambda, SourceMemberKind.Lambda, GetContainingType(lambda), display, display, display));
        }

        return members
            .OrderBy(member => member.StartLine)
            .ThenBy(member => member.EndLine)
            .ToList();
    }

    private static SourceMember CreateMember(
        string fullPath,
        string relativePath,
        SyntaxNode node,
        SourceMemberKind kind,
        string containingType,
        string name,
        string displayName,
        string displaySignature)
    {
        var span = node.SyntaxTree.GetLineSpan(node.Span);
        return new SourceMember(
            fullPath,
            relativePath,
            kind,
            containingType,
            name,
            displayName,
            displaySignature,
            span.StartLinePosition.Line + 1,
            span.EndLinePosition.Line + 1);
    }

    private static string GetContainingType(SyntaxNode node)
    {
        var type = node.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
        return type?.Identifier.Text ?? "Unknown Type";
    }

    private static string FindParentMemberName(SyntaxNode node)
    {
        foreach (var ancestor in node.Ancestors())
        {
            if (ancestor is MethodDeclarationSyntax method)
            {
                return method.Identifier.Text;
            }

            if (ancestor is ConstructorDeclarationSyntax constructor)
            {
                return constructor.Identifier.Text;
            }

            if (ancestor is PropertyDeclarationSyntax property)
            {
                return property.Identifier.Text;
            }

            if (ancestor is LocalFunctionStatementSyntax localFunction)
            {
                return localFunction.Identifier.Text;
            }
        }

        return "member";
    }

    private static string Signature(MethodDeclarationSyntax method)
    {
        return $"{method.Identifier.Text}{method.TypeParameterList}{method.ParameterList}";
    }

    private static string Signature(ConstructorDeclarationSyntax constructor)
    {
        return $"{constructor.Identifier.Text}{constructor.ParameterList}";
    }

    private static string Signature(LocalFunctionStatementSyntax localFunction)
    {
        return $"{localFunction.Identifier.Text}{localFunction.TypeParameterList}{localFunction.ParameterList}";
    }
}
