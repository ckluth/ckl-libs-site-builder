using NUnit.Framework;

namespace CKL.Libs.SiteBuilder.Tests;

public class SiteBuilderInfoTests
{
    [Test]
    public void Version_IsSet()
    {
        Assert.That(SiteBuilderInfo.Version, Is.EqualTo("1.0.0"));
    }
}
