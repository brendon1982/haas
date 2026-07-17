using Microsoft.Extensions.AI;
using HaaS.Adapters.Agent;
using HaaS.Domain.Ports;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Linq;
using System.Reflection;
using System;
using System.Linq.Expressions;

namespace HaaS.Adapters.Tests;

[TestFixture]
public class ReproductionTests
{
    [Test]
    public void AIFunctionFactory_Create_ShouldNotThrow_WhenUsingToolFromProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<MyTool>();
        var sp = services.BuildServiceProvider();
        var scopeAccessor = new FakeScopeAccessor { ServiceProvider = sp };
        var toolProvider = new ToolProvider(scopeAccessor);

        // This uses the generic Register<T> which builds an Expression-based wrapper
        toolProvider.Register<MyTool>("my_tool", "description", t => (Func<string, Task<string>>)t.ExecuteAsync);

        var tool = toolProvider.GetTools(new[] { "my_tool" }).First();

        // Act & Assert
        Assert.DoesNotThrow(() =>
        {
            if (tool.Method is not null)
            {
                AIFunctionFactory.Create(tool.Method, (Type serviceType) => toolProvider.GetService(serviceType), new AIFunctionFactoryOptions
                {
                    Name = tool.Name,
                    Description = tool.Description
                });
            }
            else
            {
                AIFunctionFactory.Create(tool.Handler, new AIFunctionFactoryOptions
                {
                    Name = tool.Name,
                    Description = tool.Description
                });
            }
        });
    }

    public class MyTool
    {
        public Task<string> ExecuteAsync(string input) => Task.FromResult(input);
    }

    private class FakeScopeAccessor : ISignalScopeAccessor
    {
        public IServiceProvider ServiceProvider { get; set; }
    }
}
