﻿// <copyright file="QnaServiceProvider.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace Microsoft.Teams.Apps.FAQPlusPlus.Common.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.CognitiveServices.Knowledge.QnAMaker;
    using Microsoft.Azure.CognitiveServices.Knowledge.QnAMaker.Models;
    using Microsoft.Extensions.Options;
    using Microsoft.Teams.Apps.FAQPlusPlus.Common.Models;
    using Microsoft.Teams.Apps.FAQPlusPlus.Common.Models.Configuration;

    /// <summary>
    /// Qna maker service provider class.
    /// </summary>
    public class QnaServiceProvider : IQnaServiceProvider
    {
        /// <summary>
        /// Environment type.
        /// </summary>
        private const string EnvironmentType = "Prod";

        private readonly IConfigurationDataProvider configurationProvider;
        private readonly IQnAMakerClient qnaMakerClient;
        private readonly IQnAMakerRuntimeClient qnaMakerRuntimeClient;
        private readonly double scoreThreshold;

        /// <summary>
        /// Represents a set of key/value application configuration properties.
        /// </summary>
        private readonly QnAMakerSettings options;

        /// <summary>
        /// Initializes a new instance of the <see cref="QnaServiceProvider"/> class.
        /// </summary>
        /// <param name="configurationProvider">ConfigurationProvider fetch and store information in storage table.</param>
        /// <param name="optionsAccessor">A set of key/value application configuration properties.</param>
        /// <param name="qnaMakerClient">Qna service client.</param>
        /// <param name="qnaMakerRuntimeClient">Qna service runtime client.</param>
        public QnaServiceProvider(IConfigurationDataProvider configurationProvider, IOptionsMonitor<QnAMakerSettings> optionsAccessor, IQnAMakerClient qnaMakerClient, IQnAMakerRuntimeClient qnaMakerRuntimeClient)
        {
            this.configurationProvider = configurationProvider;
            this.qnaMakerClient = qnaMakerClient;
            this.options = optionsAccessor.CurrentValue;
            this.qnaMakerRuntimeClient = qnaMakerRuntimeClient;
            this.scoreThreshold = Convert.ToDouble(this.options != null ? this.options.ScoreThreshold : string.Empty, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="QnaServiceProvider"/> class.
        /// </summary>
        /// <param name="configurationProvider">ConfigurationProvider fetch and store information in storage table.</param>
        /// <param name="optionsAccessor">A set of key/value application configuration properties.</param>
        /// <param name="qnaMakerClient">Qna service client.</param>
        public QnaServiceProvider(IConfigurationDataProvider configurationProvider, IOptionsMonitor<QnAMakerSettings> optionsAccessor, IQnAMakerClient qnaMakerClient)
        {
            this.configurationProvider = configurationProvider;
            this.qnaMakerClient = qnaMakerClient;
            this.options = optionsAccessor.CurrentValue;
            this.scoreThreshold = Convert.ToDouble(this.options != null ? this.options?.ScoreThreshold : string.Empty, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// This method is used to add QnA pair in Kb.
        /// </summary>
        /// <param name="question">Question text.</param>
        /// <param name="combinedDescription">Answer text.</param>
        /// <param name="createdBy">Created by user.</param>
        /// <param name="conversationId">Conversation id.</param>
        /// <param name="activityReferenceId">Activity reference id refer to activityid in storage table.</param>
        /// <returns>Operation state as task.</returns>
        public async Task<Operation> AddQnaAsync(string question, string combinedDescription, string createdBy, string conversationId, string activityReferenceId)
        {
            var knowledgeBase = await this.GetKnowledgeBaseAsync(ConfigurationEntityTypes.KnowledgeBaseId).ConfigureAwait(false);

            // Update knowledgebase.
            return await this.qnaMakerClient.Knowledgebase.UpdateAsync(knowledgeBase.Data, new UpdateKbOperationDTO
            {
                // Create JSON of changes.
                Add = new UpdateKbOperationDTOAdd
                {
                    QnaList = new List<QnADTO>
                    {
                         new QnADTO
                         {
                            Questions = new List<string> { question?.Trim() },
                            Answer = combinedDescription?.Trim(),
                            Metadata = new List<MetadataDTO>()
                            {
                                new MetadataDTO() { Name = Constants.MetadataCreatedAt, Value = DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture) },
                                new MetadataDTO() { Name = Constants.MetadataCreatedBy, Value = createdBy },
                                new MetadataDTO() { Name = Constants.MetadataConversationId, Value = conversationId?.Split(':').Last() },
                                new MetadataDTO() { Name = Constants.MetadataActivityReferenceId, Value = activityReferenceId },
                            },
                         },
                    },
                },
                Update = null,
                Delete = null,
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// This method is used to update Qna pair in Kb.
        /// </summary>
        /// <param name="questionId">Question id.</param>
        /// <param name="answer">Answer text.</param>
        /// <param name="updatedBy">Updated by user.</param>
        /// <param name="updatedQuestion">Updated question text.</param>
        /// <param name="question">Original question text.</param>
        /// <returns>Perfomed action task.</returns>
        public async Task UpdateQnaAsync(int questionId, string answer, string updatedBy, string updatedQuestion, string question)
        {
            var knowledgeBase = await this.GetKnowledgeBaseAsync(ConfigurationEntityTypes.KnowledgeBaseId).ConfigureAwait(false);
            var questions = default(UpdateQnaDTOQuestions);
            if (!string.IsNullOrEmpty(updatedQuestion?.Trim()))
            {
                questions = (updatedQuestion?.ToUpperInvariant().Trim() == question?.ToUpperInvariant().Trim()) ? null
                    : new UpdateQnaDTOQuestions()
                    {
                        Add = new List<string> { updatedQuestion.Trim() },
                        Delete = new List<string> { question.Trim() },
                    };
            }

            // Update knowledgebase.
            await this.qnaMakerClient.Knowledgebase.UpdateAsync(knowledgeBase.Data, new UpdateKbOperationDTO
            {
                // Create JSON of changes.
                Add = null,
                Update = new UpdateKbOperationDTOUpdate()
                {
                    QnaList = new List<UpdateQnaDTO>()
                    {
                        new UpdateQnaDTO()
                        {
                            Id = questionId,
                            Source = Constants.Source,
                            Answer = answer?.Trim(),
                            Questions = questions,
                            Metadata = new UpdateQnaDTOMetadata()
                            {
                                Add = new List<MetadataDTO>()
                                {
                                    new MetadataDTO() { Name = Constants.MetadataUpdatedAt, Value = DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture) },
                                    new MetadataDTO() { Name = Constants.MetadataUpdatedBy, Value = updatedBy },
                                },
                            },
                        },
                    },
                },
                Delete = null,
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// This method is used to delete Qna pair from KB.
        /// </summary>
        /// <param name="questionId">Question id.</param>
        /// <returns>Perfomed action task.</returns>
        public async Task DeleteQnaAsync(int questionId)
        {
            var knowledgeBase = await this.GetKnowledgeBaseAsync(ConfigurationEntityTypes.KnowledgeBaseId).ConfigureAwait(false);

            // to delete a question and answer based on id.
            await this.qnaMakerClient.Knowledgebase.UpdateAsync(knowledgeBase.Data, new UpdateKbOperationDTO
            {
                // Create JSON of changes.
                Add = null,
                Update = null,
                Delete = new UpdateKbOperationDTODelete()
                {
                    Ids = new List<int?>() { questionId },
                },
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Get answer from knowledgebase for a given question.
        /// </summary>
        /// <param name="question">Question text.</param>
        /// <param name="isTestKnowledgeBase">Prod or test.</param>
        /// <returns>QnaSearchResult result as response.</returns>
        public async Task<QnASearchResultList> GenerateAnswerAsync(string question, bool isTestKnowledgeBase)
        {
            var knowledgeBase = await this.GetKnowledgeBaseAsync(ConfigurationEntityTypes.KnowledgeBaseId).ConfigureAwait(false);

            QnASearchResultList qnaSearchResult = await this.qnaMakerRuntimeClient.Runtime.GenerateAnswerAsync(knowledgeBase.Data, new QueryDTO()
            {
                IsTest = isTestKnowledgeBase,
                Question = question?.Trim(),
                ScoreThreshold = this.scoreThreshold,
            }).ConfigureAwait(false);

            return qnaSearchResult;
        }

        /// <summary>
        /// This method returns the downloaded knowledgebase documents.
        /// </summary>
        /// <param name="knowledgeBaseId">Knowledgebase Id.</param>
        /// <returns>List of question and answer document object.</returns>
        public async Task<IEnumerable<QnADTO>> DownloadKnowledgebaseAsync(string knowledgeBaseId)
        {
            var qnaDocuments = await this.qnaMakerClient.Knowledgebase.DownloadAsync(knowledgeBaseId, environment: EnvironmentType).ConfigureAwait(false);
            return qnaDocuments.QnaDocuments;
        }

        /// <summary>
        /// Get knowledgebase details based on partition and row key.
        /// </summary>
        /// <param name="entityType">Entity type to get it's value from storage table.</param>
        /// <returns>Configuration entity with storage data.</returns>
        public async Task<ConfigurationEntity> GetKnowledgeBaseAsync(string entityType)
        {
            var configurationEntity = await this.configurationProvider.GetConfigurationData(Constants.ConfigurationInfoPartitionKey, entityType).ConfigureAwait(false);
            return configurationEntity;
        }

        /// <summary>
        /// Checks whether knowledgebase need to be published.
        /// </summary>
        /// <param name="knowledgeBaseId">Knowledgebase id.</param>
        /// <returns>A <see cref="Task"/> of type bool where true represents knowledgebase need to be published while false indicates knowledgebase not need to be published.</returns>
        public async Task<bool> GetPublishStatusAsync(string knowledgeBaseId)
        {
            KnowledgebaseDTO qnaDocuments = await this.qnaMakerClient.Knowledgebase.GetDetailsAsync(knowledgeBaseId).ConfigureAwait(false);
            if (qnaDocuments != null && qnaDocuments.LastChangedTimestamp != null && qnaDocuments.LastPublishedTimestamp != null)
            {
                return DateTime.Compare(Convert.ToDateTime(qnaDocuments?.LastChangedTimestamp, CultureInfo.InvariantCulture), Convert.ToDateTime(qnaDocuments?.LastPublishedTimestamp, CultureInfo.InvariantCulture)) > 0;
            }

            return true;
        }

        /// <summary>
        /// Method is used to publish knowledgebase.
        /// </summary>
        /// <param name="knowledgeBaseId">Knowledgebase Id.</param>
        /// <returns>Task for published data.</returns>
        public async Task PublishKnowledgebaseAsync(string knowledgeBaseId)
        {
            await this.qnaMakerClient.Knowledgebase.PublishAsync(knowledgeBaseId).ConfigureAwait(false);
        }

        /// <summary>
        /// Get knowledgebase published information.
        /// </summary>
        /// <param name="knowledgeBaseId">Knowledgebase id.</param>
        /// <returns>A <see cref="Task"/> of type bool where true represents knowledgebase has published atleast once while false indicates that knowledgebase has not published yet.</returns>
        public async Task<bool> GetInitialPublishedStatusAsync(string knowledgeBaseId)
        {
            KnowledgebaseDTO qnaDocuments = await this.qnaMakerClient.Knowledgebase.GetDetailsAsync(knowledgeBaseId).ConfigureAwait(false);
            return !string.IsNullOrEmpty(qnaDocuments.LastPublishedTimestamp);
        }
    }
}