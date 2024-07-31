using System.Runtime.CompilerServices;

using JetBrains.Annotations;
using JetBrains.Metadata.Reader.API;
using JetBrains.Metadata.Reader.Impl;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Colors;
using JetBrains.ReSharper.Psi.Modules;

namespace ReSharperPlugin.XnaColors;

public static class PredefinedColorTypesEx
{
    public class Data
    {
        public bool Initialized { get; internal set; }

        [CanBeNull]
        public ITypeElement XnaColorType { get; set; }
    }

    private static readonly IClrTypeName xna_color_type_name = new ClrTypeName("Microsoft.Xna.Framework.Color");

    private static readonly ConditionalWeakTable<PredefinedColorTypes, Data> cwt = new();

    internal static void Initialize([NotNull] PredefinedColorTypes @this, [NotNull] IPsiModule module)
    {
        if (@this.GetOrCreateData().Initialized)
        {
            return;
        }

        @this.GetOrCreateData().Initialized = true;

        var symbolScope = module.GetPsiServices().Symbols.GetSymbolScope(module, withReferences: true, caseSensitive: true);
        {
            @this.GetOrCreateData().XnaColorType = symbolScope.GetTypeElementByCLRName(xna_color_type_name);
        }

        if (@this.GetOrCreateData().XnaColorType is not null)
        {
            @this.ColorDefiningTypes.Add(
                new PredefinedColorTypes.ColorDefiningType(@this.GetOrCreateData().XnaColorType)
                {
                    UnderlyingColorType      = @this.GetOrCreateData().XnaColorType,
                    DefinesColorProperties   = true,
                    HasColorBuildingFunction = true,
                }
            );
        }
    }

    public static Data GetOrCreateData([NotNull] this PredefinedColorTypes @this)
    {
        return cwt.GetOrCreateValue(@this);
    }

    public static bool IsXnaColorType([NotNull] this PredefinedColorTypes @this, ITypeElement typeElement)
    {
        return @this.GetOrCreateData().XnaColorType?.Equals(typeElement) ?? false;
    }
}