using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.DependencyInjection.AppInterfaces;
using MoLibrary.DependencyInjection.CoreInterfaces;
using MoLibrary.DependencyInjection.Implements;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.DependencyInjection.Modules;

public class ModuleDependencyInjectionGuide : MoModuleGuide<ModuleDependencyInjection, ModuleDependencyInjectionOption,
    ModuleDependencyInjectionGuide>
{
  
}