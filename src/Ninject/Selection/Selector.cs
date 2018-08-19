// -------------------------------------------------------------------------------------------------
// <copyright file="Selector.cs" company="Ninject Project Contributors">
//   Copyright (c) 2007-2010 Enkari, Ltd. All rights reserved.
//   Copyright (c) 2010-2017 Ninject Project Contributors. All rights reserved.
//
//   Dual-licensed under the Apache License, Version 2.0, and the Microsoft Public License (Ms-PL).
//   You may not use this file except in compliance with one of the Licenses.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//   or
//       http://www.microsoft.com/opensource/licenses.mspx
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.
// </copyright>
// -------------------------------------------------------------------------------------------------

namespace Ninject.Selection
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    using Ninject.Components;
    using Ninject.Infrastructure;
    using Ninject.Infrastructure.Language;
    using Ninject.Selection.Heuristics;

    /// <summary>
    /// Selects members for injection.
    /// </summary>
    public class Selector : NinjectComponent, ISelector
    {
        /// <summary>
        /// The default binding flags.
        /// </summary>
        private const BindingFlags DefaultFlags = BindingFlags.Public | BindingFlags.Instance;

        /// <summary>
        /// The constructor scorer.
        /// </summary>
        private readonly IConstructorScorer constructorScorer;

        /// <summary>
        /// The injection heuristics.
        /// </summary>
        private readonly ICollection<IInjectionHeuristic> injectionHeuristics;

        /// <summary>
        /// The ninject settings.
        /// </summary>
        private readonly INinjectSettings settings;

        /// <summary>
        /// Initializes a new instance of the <see cref="Selector"/> class.
        /// </summary>
        /// <param name="constructorScorer">The constructor scorer.</param>
        /// <param name="injectionHeuristics">The injection heuristics.</param>
        /// <param name="settings">The ninject settings.</param>
        public Selector(INinjectSettings settings, IConstructorScorer constructorScorer, IEnumerable<IInjectionHeuristic> injectionHeuristics)
        {
            Ensure.ArgumentNotNull(constructorScorer, "constructorScorer");
            Ensure.ArgumentNotNull(injectionHeuristics, "injectionHeuristics");
            Ensure.ArgumentNotNull(settings, "settings");

            this.constructorScorer = constructorScorer;
            this.injectionHeuristics = injectionHeuristics.ToList();
            this.settings = settings;
        }

        /// <summary>
        /// Gets the default binding flags.
        /// </summary>
        protected virtual BindingFlags Flags
        {
            get
            {
#if !NO_LCG
                return this.settings.InjectNonPublic ? (DefaultFlags | BindingFlags.NonPublic) : DefaultFlags;
#else
                return DefaultFlags;
#endif
            }
        }

        /// <summary>
        /// Selects the constructor to call on the specified type, by using the constructor scorer.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>The selected constructor, or <see langword="null"/> if none were available.</returns>
        public virtual IEnumerable<ConstructorInfo> SelectConstructorsForInjection(Type type)
        {
            Ensure.ArgumentNotNull(type, "type");

            if (type.IsSubclassOf(typeof(MulticastDelegate)))
            {
                return null;
            }

            var constructors = type.GetConstructors(this.Flags);
            return constructors.Length == 0 ? null : constructors;
        }

        /// <summary>
        /// Selects properties that should be injected.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>A series of the selected properties.</returns>
        public virtual IEnumerable<PropertyInfo> SelectPropertiesForInjection(Type type)
        {
            Ensure.ArgumentNotNull(type, "type");

            var properties = new List<PropertyInfo>();
            properties.AddRange(
                type.GetProperties(this.Flags)
                    .Select(p => p.GetPropertyFromDeclaredType(p, this.Flags))
                    .Where(p => this.injectionHeuristics.Any(h => p != null && h.ShouldInject(p))));

#if !NO_LCG
            if (this.settings.InjectParentPrivateProperties)
            {
                for (Type parentType = type.BaseType; parentType != null; parentType = parentType.BaseType)
                {
                    properties.AddRange(this.GetPrivateProperties(parentType));
                }
            }
#endif
            return properties;
        }

        /// <summary>
        /// Selects methods that should be injected.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>A series of the selected methods.</returns>
        public virtual IEnumerable<MethodInfo> SelectMethodsForInjection(Type type)
        {
            Ensure.ArgumentNotNull(type, "type");

            return type.GetMethods(this.Flags).Where(m => this.injectionHeuristics.Any(h => h.ShouldInject(m)));
        }

#if !NO_LCG
        private IEnumerable<PropertyInfo> GetPrivateProperties(Type type)
        {
            return type.GetProperties(this.Flags).Where(p => p.DeclaringType == type && p.IsPrivate())
                .Where(p => this.injectionHeuristics.Any(h => h.ShouldInject(p)));
        }
#endif
    }
}