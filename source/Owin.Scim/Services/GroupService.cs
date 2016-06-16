﻿namespace Owin.Scim.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;

    using Microsoft.FSharp.Core;
    
    using Configuration;
    using Extensions;
    using ErrorHandling;
    using Model;
    using Model.Groups;

    using NContext.Extensions;

    using Querying;

    using Repository;
    using Validation;

    public class GroupService : ServiceBase, IGroupService
    {
        private readonly ICanonicalizationService _CanonicalizationService;

        private readonly IResourceValidatorFactory _ResourceValidatorFactory;

        private readonly IGroupRepository _GroupRepository;

        public GroupService(
            ScimServerConfiguration serverConfiguration,
            IResourceVersionProvider versionProvider,
            IResourceValidatorFactory resourceValidatorFactory,
            ICanonicalizationService canonicalizationService,
            IGroupRepository groupRepository) 
            : base(serverConfiguration, versionProvider)
        {
            _GroupRepository = groupRepository;
            _ResourceValidatorFactory = resourceValidatorFactory;
            _CanonicalizationService = canonicalizationService;
        }

        public async Task<IScimResponse<ScimGroup>> CreateGroup(ScimGroup group)
        {
            _CanonicalizationService.Canonicalize(group, ServerConfiguration.GetScimTypeDefinition(group.GetType()));

            var validator = await _ResourceValidatorFactory.CreateValidator(group);
            var validationResult = (await validator.ValidateCreateAsync(group)).ToScimValidationResult();

            if (!validationResult)
                return new ScimErrorResponse<ScimGroup>(validationResult.Errors.First());

            var createdDate = DateTime.UtcNow;
            group.Meta = new ResourceMetadata(ScimConstants.ResourceTypes.Group)
            {
                Created = createdDate,
                LastModified = createdDate
            };
            
            var groupRecord = await _GroupRepository.CreateGroup(group);

            SetResourceVersion(groupRecord);

            return new ScimDataResponse<ScimGroup>(groupRecord);
        }

        public async Task<IScimResponse<ScimGroup>> RetrieveGroup(string groupId)
        {
            var userRecord = SetResourceVersion(await _GroupRepository.GetGroup(groupId));
            if (userRecord == null)
                return new ScimErrorResponse<ScimGroup>(
                    new ScimError(
                        HttpStatusCode.NotFound,
                        detail: ScimErrorDetail.NotFound(groupId)));

            return new ScimDataResponse<ScimGroup>(userRecord);
        }

        public async Task<IScimResponse<ScimGroup>> UpdateGroup(ScimGroup group)
        {
            return await (await RetrieveGroup(group.Id))
                .BindAsync<ScimGroup, ScimGroup>(async groupRecord =>
                {
                    group.Meta = new ResourceMetadata(ScimConstants.ResourceTypes.Group)
                    {
                        Created = groupRecord.Meta.Created,
                        LastModified = groupRecord.Meta.LastModified
                    };

                    _CanonicalizationService.Canonicalize(group, ServerConfiguration.GetScimTypeDefinition(group.GetType()));

                    var validator = await _ResourceValidatorFactory.CreateValidator(group);
                    var validationResult = (await validator.ValidateUpdateAsync(group, groupRecord)).ToScimValidationResult();

                    if (!validationResult)
                        return new ScimErrorResponse<ScimGroup>(validationResult.Errors.First());

                    SetResourceVersion(group);

                    // if both versions are equal, bypass persistence
                    if (group.Meta.Version.Equals(groupRecord.Meta.Version))
                        return new ScimDataResponse<ScimGroup>(groupRecord);

                    group.Meta.LastModified = DateTime.UtcNow;

                    var updatedGroup = await _GroupRepository.UpdateGroup(group);

                    return new ScimDataResponse<ScimGroup>(updatedGroup);
                });
        }

        public async Task<IScimResponse<Unit>> DeleteGroup(string groupId)
        {
            var groupExists = await _GroupRepository.GroupExists(groupId);
            if (!groupExists)
                return new ScimErrorResponse<Unit>(
                    new ScimError(
                        HttpStatusCode.NotFound,
                        detail: ScimErrorDetail.NotFound(groupId)));

            await _GroupRepository.DeleteGroup(groupId);

            return new ScimDataResponse<Unit>(default(Unit));
        }

        public async Task<IScimResponse<IEnumerable<ScimGroup>>> QueryGroups(ScimQueryOptions options)
        {
            var groups = await _GroupRepository.QueryGroups(options) ?? new List<ScimGroup>();
            groups.ForEach(group => SetResourceVersion(group));

            return new ScimDataResponse<IEnumerable<ScimGroup>>(groups);
        }
    }
}