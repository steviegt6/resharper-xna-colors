using System.Collections.Generic;
using System.Linq;

using JetBrains.Annotations;
using JetBrains.Metadata.Reader.API;
using JetBrains.Metadata.Reader.Impl;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Colors;
using JetBrains.ReSharper.Psi.Modules;

namespace ReSharperPlugin.XnaColors;

public class PredefinedColorTypesEx
{
    private static readonly IClrTypeName xna_color_type_name = new ClrTypeName("Microsoft.Xna.Framework.Color");

    [CanBeNull]
    public ITypeElement XnaColorType { get; }

    public ICollection<PredefinedColorTypes.ColorDefiningType> ColorDefiningTypes { get; }

    internal PredefinedColorTypesEx([NotNull] IPsiModule module)
    {
        var symbolScope = module.GetPsiServices().Symbols.GetSymbolScope(module, withReferences: true, caseSensitive: true);
        {
            XnaColorType = symbolScope.GetTypeElementByCLRName(xna_color_type_name);
        }

        ColorDefiningTypes = new List<PredefinedColorTypes.ColorDefiningType>
        {
            new(XnaColorType)
            {
                UnderlyingColorType      = XnaColorType,
                DefinesColorProperties   = true,
                HasColorBuildingFunction = true,
            },
        }.AsReadOnly();
        ColorDefiningTypes = ColorDefiningTypes.Where(x => x.UnderlyingColorType is not null && x.ColorProvidingType is not null).ToList();
    }

    public static PredefinedColorTypesEx Get([NotNull] IPsiModule module)
    {
        // TODO
        return null;
    }

    public bool IsXnaColorType(ITypeElement typeElement)
    {
        return XnaColorType is not null && XnaColorType.Equals(typeElement);
    }
}