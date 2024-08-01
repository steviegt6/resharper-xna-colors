using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

using JetBrains;
using JetBrains.Annotations;
using JetBrains.Application;
using JetBrains.Application.Notifications;
using JetBrains.Diagnostics;
using JetBrains.Lifetimes;
using JetBrains.ReSharper.Feature.Services.CSharp.VisualElements;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Colors;
using JetBrains.ReSharper.Psi.CSharp.Resolve;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Modules;
using JetBrains.ReSharper.Psi.Resolve;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.Util;
using JetBrains.Util.Media;
using JetBrains.Util.Special;

using MonoMod.RuntimeDetour;

namespace ReSharperPlugin.XnaColors;

// TODO: I'm not very familiar with the R# SDK.
// Can I avoid patching here and reimplement the feature in a reasonable manner?

[ShellComponent]
public class XnaColorsComponent
{
    // ReSharper disable once CollectionNeverQueried.Local - Used to preserve
    //                                                       hook lifetimes.
    private static readonly List<object> lifetime_extender = [];

    public XnaColorsComponent(Lifetime lifetime, UserNotifications notifications)
    {
        lifetime_extender.Add(
            new Hook(
                typeof(PredefinedColorTypes).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(IPsiModule)], null)!,
                //
                typeof(XnaColorsComponent).GetMethod(nameof(Constructor), BindingFlags.Static | BindingFlags.NonPublic)!
            )
        );

        lifetime_extender.Add(
            new Hook(
                typeof(PredefinedColorTypes).GetMethod(nameof(GetQualifierType), BindingFlags.Static | BindingFlags.NonPublic)!,
                typeof(XnaColorsComponent).GetMethod(nameof(GetQualifierType), BindingFlags.Static   | BindingFlags.NonPublic)!
            )
        );

        lifetime_extender.Add(
            new Hook(
                typeof(PredefinedColorTypes).GetMethod(nameof(ColorReferenceFromInvocation), BindingFlags.Static | BindingFlags.Public)!,
                typeof(XnaColorsComponent).GetMethod(nameof(ColorReferenceFromInvocation), BindingFlags.Static   | BindingFlags.NonPublic)!
            )
        );

        lifetime_extender.Add(
            new Hook(
                typeof(PredefinedColorTypes).GetMethod(nameof(TryReplaceAsInvocation), BindingFlags.Instance | BindingFlags.Public)!,
                typeof(XnaColorsComponent).GetMethod(nameof(TryReplaceAsInvocation), BindingFlags.Static     | BindingFlags.NonPublic)!
            )
        );

        var visualElementFactoryType = Type.GetType("JetBrains.ReSharper.Feature.Services.CSharp.VisualElements.VisualElementFactory, JetBrains.ReSharper.Feature.Services.CSharp")!;
        lifetime_extender.Add(
            new Hook(
                visualElementFactoryType.GetMethod(nameof(GetColorReference), BindingFlags.Instance | BindingFlags.Public)!,
                typeof(XnaColorsComponent).GetMethod(nameof(GetColorReference), BindingFlags.Static | BindingFlags.NonPublic)!
            )
        );

        notifications.CreateNotification(lifetime, NotificationSeverity.INFO, "XNA Color Hooks Applied", "ReSharper has been patched to include support for XNA colors!");
    }

    private static void Constructor(Action<PredefinedColorTypes, IPsiModule> orig, [NotNull] PredefinedColorTypes self, [NotNull] IPsiModule module)
    {
        orig(self, module);
        PredefinedColorTypesEx.Initialize(self, module);
    }

    private static ITypeElement GetQualifierType(Func<IReference, ITypeElement> orig, [NotNull] IReference qualifierReference)
    {
        if (qualifierReference.Resolve().DeclaredElement is not ITypeElement typeElement)
        {
            return orig(qualifierReference);
        }

        var predefinedColorTypes = PredefinedColorTypes.Get(qualifierReference.GetTreeNode().GetPsiModule());
        return predefinedColorTypes.IsXnaColorType(typeElement) ? typeElement : orig(qualifierReference);
    }

    [CanBeNull]
    private static IColorReference ColorReferenceFromInvocation(
        Func<StringComparer, IReference, IReference, IArgument[], Func<ITypeElement, IColorElement, IColorReference>, IColorReference> orig,
        [NotNull] StringComparer                                                                                                       comparer,
        [NotNull] IReference                                                                                                           qualifierReference,
        [NotNull] IReference                                                                                                           methodReference,
        [NotNull] IArgument[]                                                                                                          args,
        [NotNull] Func<ITypeElement, IColorElement, IColorReference>                                                                   factory
    )
    {
        return orig(comparer, qualifierReference, methodReference, args, factory);
    }

    private static void TryReplaceAsInvocation(
        Action<PredefinedColorTypes, ITypeElement, IColorElement, PredefinedColorTypes.LanguageSpecificReplaceInvocation> orig,
        [NotNull] PredefinedColorTypes                                                                                    self,
        ITypeElement                                                                                                      qualifierType,
        IColorElement                                                                                                     colorElement,
        PredefinedColorTypes.LanguageSpecificReplaceInvocation                                                            replace
    )
    {
        var colorType = self.UnderlyingColorType(qualifierType);
        if (self.IsXnaColorType(colorType))
        {
            // TODO
        }
        else
        {
            orig(self, qualifierType, colorElement, replace);
        }
    }

    private static IColorReference GetColorReference(
        Func<VisualElementFactory, ITreeNode, IColorReference> orig,
        [NotNull] VisualElementFactory                         self,
        ITreeNode                                              element
    )
    {
        if (orig(self, element) is { } ret)
        {
            return ret;
        }

        if (element is IObjectCreationExpression creationExpression)
        {
            return ReferenceFromConstructor(creationExpression, creationExpression.Reference);
        }

        /*if (element is not IReferenceExpression referenceExpression)
        {
            return null;
        }

        if (referenceExpression.QualifierExpression is not IReferenceExpression qualifier)
        {
            return null;
        }*/

        return null;
    }

    private static IColorReference ReferenceFromConstructor(IObjectCreationExpression expression, ICSharpInvocationReference reference)
    {
        /*if (reference.Resolve().DeclaredElement is not Constructor ctor)
        {
            return null;
        }

        if (PredefinedColorTypes.GetQualifierType(ctor.ContainingType) is not { } colorElement)
        {
            return null;
        }*/

        if (reference.Resolve().DeclaredElement is not IConstructor ctor)
        {
            return null;
        }

        var invocation           = reference.Invocation;
        var args                 = invocation.Arguments.Cast<IArgument>().ToList();

        if (PredefinedColorTypes.Get(expression.GetPsiModule()).IsXnaColorType(ctor.ContainingType))
        {
            // RGB
            if (args.CountIs(3))
            {
                if (invocation.Reference?.Resolve().DeclaredElement is not IFunction func || !func.Parameters.CountIs(3))
                {
                    return null;
                }

                var color = BuildBaseColorWithAlphaIntConstant(args);
                color ??= BuildBaseColorWithAlphaFloatConstant(args);

                if (!color.HasValue)
                {
                    return null;
                }

                return new MyColorReference(new ColorElement(color.Value, null), ctor.ContainingType, expression);
            }

            // RGBA
            if (invocation.Arguments.CountIs(4))
            {
                if (invocation.Reference?.Resolve().DeclaredElement is not IFunction func || !func.Parameters.CountIs(4))
                {
                    return null;
                }

                var color = BuildBaseColorWithAlphaIntConstant(args);
                color ??= BuildBaseColorWithAlphaFloatConstant(args);

                if (!color.HasValue)
                {
                    return null;
                }

                return new MyColorReference(new ColorElement(color.Value, null), ctor.ContainingType, expression);
            }
        }

        return null;
    }

    private static JetRgbaColor? BuildBaseColorWithAlphaIntConstant(IList<IArgument> args)
    {
        int? a = GetArgumentAsIntConstant(args, "alpha", 0, 255) ?? GetArgumentAsIntConstant(args, "a", 0, 255);
        int? r = GetArgumentAsIntConstant(args, "red",   0, 255) ?? GetArgumentAsIntConstant(args, "r", 0, 255);
        int? g = GetArgumentAsIntConstant(args, "green", 0, 255) ?? GetArgumentAsIntConstant(args, "g", 0, 255);
        int? b = GetArgumentAsIntConstant(args, "blue",  0, 255) ?? GetArgumentAsIntConstant(args, "b", 0, 255);

        if (!r.HasValue || !g.HasValue || !b.HasValue)
        {
            return null;
        }

        return JetRgbaColor.FromRgba((byte)r.Value, (byte)g.Value, (byte)b.Value, (byte)(a ?? 255));
    }

    private static JetRgbaColor? BuildBaseColorWithAlphaFloatConstant(IList<IArgument> args)
    {
        float? a = GetArgumentAsFloatConstant(args, "alpha", 0f, 1f) ?? GetArgumentAsFloatConstant(args, "a", 0f, 1f);
        float? r = GetArgumentAsFloatConstant(args, "red",   0f, 1f) ?? GetArgumentAsFloatConstant(args, "r", 0f, 1f);
        float? g = GetArgumentAsFloatConstant(args, "green", 0f, 1f) ?? GetArgumentAsFloatConstant(args, "g", 0f, 1f);
        float? b = GetArgumentAsFloatConstant(args, "blue",  0f, 1f) ?? GetArgumentAsFloatConstant(args, "b", 0f, 1f);

        if (!r.HasValue || !g.HasValue || !b.HasValue)
        {
            return null;
        }

        return JetRgbaColor.FromRgba((byte)(r.Value / 255f), (byte)(g.Value / 255f), (byte)(b.Value / 255f), (byte)((a ?? 255) / 255f));
    }

    private static int? GetArgumentAsIntConstant([NotNull] IEnumerable<IArgument> args, [NotNull] string parameterName, int min, int max)
    {
        var argument2 = args.FirstOrDefault(argument => argument.MatchingParameter.IfNotNull(parameter => parameterName.Equals(parameter.Element.ShortName, StringComparison.Ordinal)));
        if (argument2 == null)
        {
            return null;
        }

        var type       = argument2.MatchingParameter.NotNull("matchingParameter != null").Element.Type;
        var expression = argument2.Expression;
        if (expression == null)
        {
            return null;
        }

        var constantValue = expression.ConstantValue;
        if (constantValue.IsErrorOrNonCompileTimeConstantValue())
        {
            return null;
        }

        var typeConversionRule = argument2.Language.TypeConversionRule(expression);
        if (typeConversionRule == null)
        {
            return null;
        }

        if (!expression.GetExpressionType().IsImplicitlyConvertibleTo(type, typeConversionRule))
        {
            return null;
        }

        double? num = null;
        try
        {
            num = Convert.ToDouble(constantValue.Value, CultureInfo.InvariantCulture);
        }
        catch
        {
            // ignore
        }

        if (!num.HasValue || double.IsNaN(num.Value) || double.IsInfinity(num.Value) || Math.Truncate(num.Value) != num.Value || num.Value.Clamp(min, max) != num.Value)
        {
            return null;
        }
        return (int)num.Value;
    }

    private static float? GetArgumentAsFloatConstant([NotNull] IEnumerable<IArgument> args, [NotNull] string parameterName, float min, float max)
    {
        var argument2 = args.FirstOrDefault(argument => argument.MatchingParameter.IfNotNull(parameter => parameterName.Equals(parameter.Element.ShortName, StringComparison.Ordinal)));
        if (argument2 == null)
        {
            return null;
        }

        var type       = argument2.MatchingParameter.NotNull("matchingParameter != null").Element.Type;
        var expression = argument2.Expression;
        if (expression == null)
        {
            return null;
        }

        var constantValue = expression.ConstantValue;
        if (constantValue.IsErrorOrNonCompileTimeConstantValue())
        {
            return null;
        }

        var typeConversionRule = argument2.Language.TypeConversionRule(expression);
        if (typeConversionRule == null)
        {
            return null;
        }

        if (!expression.GetExpressionType().IsImplicitlyConvertibleTo(type, typeConversionRule))
        {
            return null;
        }

        double? num = null;
        try
        {
            num = Convert.ToDouble(constantValue.Value, CultureInfo.InvariantCulture);
        }
        catch
        {
            // ignore
        }

        if (!num.HasValue || double.IsNaN(num.Value) || double.IsInfinity(num.Value) || Math.Truncate(num.Value) != num.Value || num.Value.Clamp(min, max) != num.Value)
        {
            return null;
        }
        return (float)num.Value;
    }
}