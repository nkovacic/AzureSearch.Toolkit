﻿using Azure.Search.Documents.Models;
using AzureSearchToolkit.Request.Criteria;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AzureSearchToolkit.Request.Formatters
{
    /// <summary>
    /// Formats a SearchRequest to be sent to AzureSearch.
    /// </summary>

    class SearchRequestFormatter
    {
        /// <summary>
        /// The formatted SearchRequest sent to AzureSearch.
        /// </summary>
        public AzureSearchRequest SearchRequest { get; }

        public SearchRequestFormatter(AzureSearchRequest searchRequest)
        {
            SearchRequest = searchRequest;

            Build(searchRequest.Criteria);
        }

        private void Build(ICriteria criteria)
        {
            if (criteria == null)
            {
                return;
            }

            if (criteria is ExistsCriteria)
            {
                SimpleBuild(criteria);

                return;
            }

            if (criteria is ComparisonCriteria)
            {
                SimpleBuild(criteria);

                return;
            }

            if (criteria is DistanceCriteria)
            {
                SimpleBuild(criteria);

                return;
            }

            /*
            if (criteria is RegexpCriteria)
            {
                Build((RegexpCriteria)criteria);

                return;
            }

            if (criteria is PrefixCriteria)
            {
                Build((PrefixCriteria)criteria);

                return;
            }*/


            if (criteria is TermCriteria)
            {
                SimpleBuild(criteria);

                return;
            }

            if (criteria is TermsCriteria)
            {
                SimpleBuild(criteria);

                return;
            }

            if (criteria is QueryStringCriteria)
            {
                Build((QueryStringCriteria)criteria);

                return;
            }

            // Base class formatters using name property
            /*
            if (criteria is SingleFieldCriteria)
            {
                Build((SingleFieldCriteria)criteria);

                return;
            }*/

            if (criteria is CompoundCriteria)
            {
                Build((CompoundCriteria)criteria);

                return;
            }

            throw new InvalidOperationException($"Unknown criteria type '{criteria.GetType()}'");
        }

        private void Build(TermCriteria criteria)
        {
            SearchRequest.SearchOptions.Filter = criteria.ToString();
        }

        private void Build(TermsCriteria criteria)
        {
            SearchRequest.SearchOptions.Filter = criteria.ToString();
        }

        private void Build(QueryStringCriteria criteria)
        {
            SearchRequest.SearchOptions.QueryType = SearchQueryType.Full;
            SearchRequest.SearchText = criteria.Value;

            if (criteria.Fields?.Any() == true)
            {
                SearchRequest.AddRangeToSearchFields(criteria.Fields.ToArray());
            }
        }

        private void Build(ComparisonCriteria criteria)
        {
            SearchRequest.SearchOptions.Filter = criteria.ToString();
        }

        private void Build(CompoundCriteria criteria)
        {
            if (criteria.Criteria.Count == 1)
            {
                Build(criteria.Criteria.First());
            }
            else
            {
                var queryStringCritera = criteria.Criteria.Where(q => q is QueryStringCriteria);

                if (queryStringCritera.Any())
                {
                    if (queryStringCritera.Count() == 1)
                    {
                        Build(queryStringCritera.First());
                    }
                    else
                    {
                        throw new NotSupportedException("Multiple Contains queries on string properties are currently not supported!");
                    }

                    criteria.RemoveQueryStringCriteria();

                    Build(criteria);

                    return;
                }

                SimpleBuild(criteria);
            }
        }

        private void SimpleBuild(ICriteria criteria)
        {
            SearchRequest.SearchOptions.Filter = criteria.ToString();
        }
    }
}
