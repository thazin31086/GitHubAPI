﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace GitHubApiDemo.Descriptions.GitHubApiDemo {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "15.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class SearchRepositoriesBroker {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal SearchRepositoriesBroker() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("GitHubApiDemo.Descriptions.GitHubApiDemo.SearchRepositoriesBroker", typeof(SearchRepositoriesBroker).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to A value that determines if the search results are limited to only archived or only non-archived repositories.  The default is to include both archived and non-archived repositories..
        /// </summary>
        internal static string Archived {
            get {
                return ResourceManager.GetString("Archived", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Limit the search results to repositories created during the specified range of dates..
        /// </summary>
        internal static string Created {
            get {
                return ResourceManager.GetString("Created", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to A value that determines if the search results include forked repositories..
        /// </summary>
        internal static string Fork {
            get {
                return ResourceManager.GetString("Fork", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Limit the search results to repositories where the number of forks falls within the specified range..
        /// </summary>
        internal static string Forks {
            get {
                return ResourceManager.GetString("Forks", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Limit the search results to repositories where the specified field(s) contain the search term (Term)..
        /// </summary>
        internal static string In {
            get {
                return ResourceManager.GetString("In", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Limit the search results to repositories that match the specified programming language..
        /// </summary>
        internal static string Language {
            get {
                return ResourceManager.GetString("Language", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Limit the search results to repositories where the size (in kilobytes) falls within the specified range..
        /// </summary>
        internal static string Size {
            get {
                return ResourceManager.GetString("Size", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Sort the search results by the specified field..
        /// </summary>
        internal static string SortField {
            get {
                return ResourceManager.GetString("SortField", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Limit the search results to repositories where the number of stars falls within the specified range..
        /// </summary>
        internal static string Stars {
            get {
                return ResourceManager.GetString("Stars", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Limit the search results to repositories updated during the specified range of dates..
        /// </summary>
        internal static string Updated {
            get {
                return ResourceManager.GetString("Updated", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Limit the search results to repositories owned by the specified user..
        /// </summary>
        internal static string User {
            get {
                return ResourceManager.GetString("User", resourceCulture);
            }
        }
    }
}
