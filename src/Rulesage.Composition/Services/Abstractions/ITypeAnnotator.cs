namespace Rulesage.Composition.Services.Abstractions;

public interface ITypeAnnotator
{
    Task<string> AnnotateAsync(
        string nlStructure,
        string plan,
        CancellationToken cancellationToken = default);
}