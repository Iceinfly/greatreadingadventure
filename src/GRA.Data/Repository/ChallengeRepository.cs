﻿using System.Linq;
using Microsoft.Extensions.Logging;
using GRA.Domain.Repository;
using Microsoft.EntityFrameworkCore;
using AutoMapper.QueryableExtensions;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using GRA.Domain.Model;

namespace GRA.Data.Repository
{
    public class ChallengeRepository
        : AuditingRepository<Model.Challenge, Challenge>, IChallengeRepository
    {
        public ChallengeRepository(ServiceFacade.Repository repositoryFacade,
            ILogger<ChallengeRepository> logger) : base(repositoryFacade, logger)
        {
        }

        public async Task<IEnumerable<Challenge>>
            PageAllAsync(int siteId, int skip, int take, string search = null)
        {
            if (string.IsNullOrEmpty(search))
            {
                return await DbSet
                    .AsNoTracking()
                    .Where(_ => _.IsDeleted == false
                        && _.SiteId == siteId)
                    .OrderBy(_ => _.Name)
                    .Skip(skip)
                    .Take(take)
                    .ProjectTo<Challenge>()
                    .ToListAsync();
            }
            else
            {
                return await DbSet
                    .AsNoTracking()
                    .Include(_ => _.Tasks)
                    .Where(_ => _.IsDeleted == false
                        && _.SiteId == siteId
                        && (_.Name.Contains(search)
                        || _.Description.Contains(search)
                        || _.Tasks.Any(_t => _t.Title.Contains(search))
                        || _.Tasks.Any(_t => _t.Author.Contains(search))))
                    .OrderBy(_ => _.Name)
                    .Skip(skip)
                    .Take(take)
                    .ProjectTo<Challenge>()
                    .ToListAsync();
            }
        }

        public async Task<int> GetChallengeCountAsync(int siteId, string search = null)
        {
            if (string.IsNullOrEmpty(search))
            {
                var challenges = await DbSet
                .AsNoTracking()
                .Where(_ => _.IsDeleted == false && _.SiteId == siteId)
                .ToListAsync();
                return challenges.Count();
            }
            else
            {
                var challenges = await DbSet
                .AsNoTracking()
                .Include(_ => _.Tasks)
                .Where(_ => _.IsDeleted == false
                    && _.SiteId == siteId
                        && (_.Name.Contains(search)
                        || _.Description.Contains(search)
                        || _.Tasks.Any(_t => _t.Title.Contains(search))
                        || _.Tasks.Any(_t => _t.Author.Contains(search))))
                .ToListAsync();
                return challenges.Count();
            }

        }

        public async Task<Challenge> GetByIdAsync(int id, int? userId = null)
        {
            var challenge = _mapper.Map<Model.Challenge, Challenge>(await DbSet
                .AsNoTracking()
                .Where(_ => _.IsDeleted == false && _.Id == id)
                .SingleAsync());

            if (challenge != null)
            {
                challenge.Tasks = await _context.ChallengeTasks
                    .AsNoTracking()
                    .Where(_ => _.ChallengeId == id)
                    .OrderBy(_ => _.Position)
                    .ProjectTo<ChallengeTask>()
                    .ToListAsync();

                await GetChallengeTasksTypeAsync(challenge.Tasks);
            }

            if (userId != null)
            {
                // determine if challenge is completed
                var challengeStatus = await _context.UserLogs
                    .AsNoTracking()
                    .Where(_ => _.UserId == userId && _.ChallengeId == id)
                    .SingleOrDefaultAsync();
                if (challengeStatus != null)
                {
                    challenge.IsCompleted = true;
                    challenge.CompletedAt = challengeStatus.CreatedAt;
                }

                var userChallengeTasks = await _context.UserChallengeTasks
                    .AsNoTracking()
                    .Where(_ => _.UserId == (int)userId)
                    .ToListAsync();

                foreach (var userChallengeTask in userChallengeTasks)
                {
                    var task = challenge.Tasks
                        .Where(_ => _.Id == userChallengeTask.ChallengeTaskId)
                        .SingleOrDefault();
                    if (task != null && userChallengeTask.IsCompleted)
                    {
                        task.IsCompleted = true;
                        task.CompletedAt = userChallengeTask.CreatedAt;
                    }
                }
            }

            return challenge;
        }

        public override async Task RemoveSaveAsync(int userId, int id)
        {
            var entity = await _context.Challenges
                .Where(_ => _.IsDeleted == false && _.Id == id)
                .SingleAsync();
            entity.IsDeleted = true;
            await base.UpdateAsync(userId, entity, null);
            await base.SaveAsync();
        }

        public async Task<IEnumerable<ChallengeTask>>
            GetChallengeTasksAsync(int challengeId, int? userId = null)
        {
            var tasks = await _context.ChallengeTasks
                .AsNoTracking()
                .Where(_ => _.ChallengeId == challengeId)
                .OrderBy(_ => _.Position)
                .ProjectTo<ChallengeTask>()
                .ToListAsync();

            return await GetChallengeTasksTypeAsync(tasks);
        }

        private async Task<IEnumerable<ChallengeTask>>
            GetChallengeTasksTypeAsync(IEnumerable<ChallengeTask> tasks)
        {
            var challengeTaskTypes =
                await _context.ChallengeTaskTypes
                .AsNoTracking()
                .ToDictionaryAsync(_ => _.Id);

            foreach (var task in tasks)
            {
                task.ChallengeTaskType = (ChallengeTaskType)
                    Enum.Parse(typeof(ChallengeTaskType),
                    challengeTaskTypes[task.ChallengeTaskTypeId].Name);
            }
            return tasks;
        }

        public async Task UpdateUserChallengeTask(int userId,
            IEnumerable<ChallengeTask> challengeTasks)
        {
            foreach (var updatedChallengeTask in challengeTasks)
            {
                var savedChallengeTask = await _context
                    .UserChallengeTasks.Where(_ => _.UserId == userId
                     && _.ChallengeTaskId == updatedChallengeTask.Id)
                     .SingleOrDefaultAsync();

                if (savedChallengeTask == null)
                {
                    _context.UserChallengeTasks.Add(new Model.UserChallengeTask
                    {
                        ChallengeTaskId = updatedChallengeTask.Id,
                        UserId = userId,
                        IsCompleted = updatedChallengeTask.IsCompleted ?? false
                    });
                }
                else
                {
                    savedChallengeTask.IsCompleted = updatedChallengeTask.IsCompleted ?? false;
                    _context.UserChallengeTasks.Update(savedChallengeTask);
                }
            }
            await SaveAsync();
        }
    }
}