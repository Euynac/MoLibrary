namespace MoLibrary.DomainDrivenDesign.AutoController.Attributes;

[AttributeUsage(AttributeTargets.Assembly)]
public class AutoControllerGeneratorClientConfigAttribute : Attribute
{
    public bool AddGrpcImplementations { get; set; }
}