using System;
using System.Collections.Generic;
using System.Reflection;

using JetBrains.Annotations;
using JetBrains.Application;
using JetBrains.Application.Notifications;
using JetBrains.Lifetimes;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Colors;
using JetBrains.ReSharper.Psi.CSharp.Resolve;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Modules;
using JetBrains.ReSharper.Psi.Resolve;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.Util;

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
        Func<object /*VisualElementFactory*/, ITreeNode, IColorReference> orig,
        [NotNull] object /*VisualElementFactory*/                         self,
        ITreeNode                                                         element
    )
    {
        if (orig(self, element) is { } ret)
        {
            return ret;
        }

        if (element is IObjectCreationExpression creationExpression)
        {
            return ReferenceFromConstructor(creationExpression.Reference);
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

    private static IColorReference ReferenceFromConstructor(ICSharpInvocationReference reference)
    {
        if (PredefinedColorTypes.GetQualifierType(reference))
        
        var invocation = reference.Invocation;
        if (!invocation.Arguments.CountIs(3) && !invocation.Arguments.CountIs(4))
        {
            return null;
        }
        return null;
    }
}