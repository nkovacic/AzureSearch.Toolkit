﻿using Microsoft.Spatial;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace AzureSearchToolkit
{
    /// <summary>
    /// Provides methods that stand in for additional operations available in AzureSearch.
    /// </summary>
    public static class AzureSearchMethods
    {
        /// <summary>
        /// Determines whether a sequence contains any of the specified items.
        /// </summary>
        /// <typeparam name="TSource">The type of the elements of source.</typeparam>
        /// <param name="source">A sequence in which to locate one of the items.</param>
        /// <param name="items">A sequence containing the items to be located.</param>
        /// <returns>true if the source sequence contains any of the items; otherwise, false.</returns>
        public static bool ContainsAny<TSource>(IEnumerable<TSource> source, params TSource[] items)
        {
            throw BuildException();
        }

        /// <summary>
        /// Determines whether a sequence contains all of the specified items.
        /// </summary>
        /// <typeparam name="TSource">The type of the elements of source.</typeparam>
        /// <param name="source">A sequence in which to locate all of the items.</param>
        /// <param name="items">A sequence containing all the items to be located.</param>
        /// <returns>true if the source sequence contains all of the items; otherwise, false.</returns>
        public static bool ContainsAll<TSource>(IEnumerable<TSource> source, params TSource[] items)
        {
            throw BuildException();
        }

        /// <summary>
        /// Used for distance operations on sequence
        /// </summary>
        /// <param name="source"> GeographyPoint on source where operations takes place</param>
        /// <param name="filterPoint">GeographyPoint for calculating distance from source GeographyPoint</param>
        /// <returns>Distance between <paramref name="source"/> and <paramref name="filterPoint"/> in kilometers</returns>
        public static double Distance(GeographyPoint source, GeographyPoint filterPoint)
        {
            throw BuildException();
        }

        /// <summary>
        /// Create the InvalidOperationException fired when trying to execute methods of this proxy class.
        /// </summary>
        /// <param name="memberName">Optional name of the member, automatically figured out via CallerMemberName if not specified.</param>
        /// <returns>InvalidOperationException with appropriate error message.</returns>
        static InvalidOperationException BuildException([CallerMemberName] string memberName = null)
        {
            return new InvalidOperationException($"AzureSearchMethods.{memberName} is a method for mapping queries to AzureSearch and should not be called directly.");
        }
    }
}
