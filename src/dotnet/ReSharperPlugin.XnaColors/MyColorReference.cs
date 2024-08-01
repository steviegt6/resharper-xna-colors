using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using JetBrains.Annotations;
using JetBrains.DocumentModel;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Colors;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Modules;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.Util;

namespace ReSharperPlugin.XnaColors;

public sealed class MyColorReference : IColorReference
{
    public ITreeNode Owner => myOwner;

    public DocumentRange? ColorConstantRange { get; }

    public IColorElement ColorElement { get; }

    public ColorBindOptions BindOptions => myPredefinedColorTypes.BindingOptions(myQualifierType);

    private readonly ITypeElement         myQualifierType;
    private readonly ICSharpExpression    myOwner;
    private readonly PredefinedColorTypes myPredefinedColorTypes;

    public MyColorReference(IColorElement colorElement, ITypeElement qualifierType, IObjectCreationExpression expression)
    {
        ColorElement           = colorElement;
        myQualifierType        = qualifierType;
        myOwner                = expression;
        ColorConstantRange     = expression.ArgumentList.GetDocumentRange();
        myPredefinedColorTypes = PredefinedColorTypes.Get(myOwner.GetPsiModule());
    }

    public void Bind(IColorElement colorElement)
    {
        if (TryReplaceAsNamed(colorElement) is null)
        {
            myPredefinedColorTypes.TryReplaceAsInvocation(myQualifierType, colorElement, ReplaceInvocation);
        }
    }

    public IEnumerable<IColorElement> GetColorTable()
    {
        return myPredefinedColorTypes.ColorElementsByType(myQualifierType);
    }

    [CanBeNull]
    private ICSharpExpression TryReplaceAsNamed(IColorElement colorElement)
    {
        var pair = myPredefinedColorTypes.PropertyFromColorElement(myQualifierType, colorElement);
        if (!pair.HasValue)
        {
            return null;
        }
        var expression = CSharpElementFactory.GetInstance(myOwner).CreateExpression("$0.$1", pair.Value.First, pair.Value.Second);
        return myOwner.ReplaceBy(expression);
    }

    [CanBeNull]
    private ITreeNode ReplaceInvocation(IMethod fromArgb, [CanBeNull] string replaceAll, bool useHex, params Pair<string, int>[] args)
    {
        var psiModule = myOwner.GetPsiModule();
        var factory   = CSharpElementFactory.GetInstance(myOwner);
        var source    = args.Select(arg => Pair.Of(arg.First, CreateConstantExpression(arg.Second, useHex, factory, psiModule))).ToList();

        if (myOwner is IInvocationExpression invocationExpression)
        {
            var invocationExpressionReference = invocationExpression.InvocationExpressionReference;
            if (fromArgb.Equals(invocationExpressionReference.Resolve().DeclaredElement))
            {
                var list = source.Select(arg => new { Argument = FindArgument(invocationExpression, arg.First), Value = arg.Second }).Where(arg => arg.Argument != null).ToList();
                if (list.Count == args.Length)
                {
                    foreach (var item in list)
                    {
                        item.Argument.SetValue(item.Value);
                    }
                    return invocationExpression;
                }
            }
        }
        if (replaceAll == null)
        {
            return null;
        }
        var expression = factory.CreateExpression(replaceAll, source.Select((Func<Pair<string, ICSharpExpression>, object>)(pair => pair.Second)).Prepend(fromArgb).ToArray());
        return myOwner.ReplaceBy(expression) as IInvocationExpression;
    }

    [CanBeNull]
    private static ICSharpExpression CreateConstantExpression(int value, bool hex, CSharpElementFactory factory, IPsiModule psiModule)
    {
        var format = value >= 0 ? "0x{0:x}" : "unchecked((int)0x{0:x})";
        if (!hex)
        {
            return factory.CreateExpressionByConstantValue(ConstantValue.Int(value, psiModule));
        }
        return factory.CreateExpression(string.Format(CultureInfo.InvariantCulture, format, value));
    }

    [CanBeNull]
    private static ICSharpArgument FindArgument([NotNull] IInvocationExpression invocationExpression, string paramName)
    {
        foreach (var argument in invocationExpression.Arguments)
        {
            var matchingParameter = argument.MatchingParameter;
            if (matchingParameter != null && paramName.Equals(matchingParameter.Element.ShortName, StringComparison.Ordinal))
            {
                return argument;
            }
        }
        return null;
    }
}