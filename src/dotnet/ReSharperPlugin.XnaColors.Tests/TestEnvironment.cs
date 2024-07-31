using System.Threading;
using JetBrains.Application.BuildScript.Application.Zones;
using JetBrains.ReSharper.Feature.Services;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.TestFramework;
using JetBrains.TestFramework;
using JetBrains.TestFramework.Application.Zones;
using NUnit.Framework;

[assembly: Apartment(ApartmentState.STA)]

namespace ReSharperPlugin.XnaColors.Tests;

[ZoneDefinition]
public class XnaColorsTestEnvironmentZone : ITestsEnvZone, IRequire<PsiFeatureTestZone>, IRequire<IXnaColorsZone>;

[ZoneMarker]
public class ZoneMarker : IRequire<ICodeEditingZone>, IRequire<ILanguageCSharpZone>, IRequire<XnaColorsTestEnvironmentZone>;

[SetUpFixture]
public class XnaColorsTestsAssembly : ExtensionTestEnvironmentAssembly<XnaColorsTestEnvironmentZone>;