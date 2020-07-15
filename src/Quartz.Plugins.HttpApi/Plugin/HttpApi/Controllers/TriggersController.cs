using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;

using Quartz.Impl;
using Quartz.Plugin.HttpApi.Contract;

namespace Quartz.Plugin.HttpApi.Controllers
{
    [Route("api/schedulers/{schedulerName}/triggers")]
    public class TriggersController : Controller
    {
        [HttpGet]
        [Route("")]
        public async Task<IReadOnlyList<KeyDto>> Triggers(string schedulerName, GroupMatcherDto groupMatcher)
        {
            var scheduler = await GetScheduler(schedulerName).ConfigureAwait(false);
            var matcher = (groupMatcher ?? new GroupMatcherDto()).GetTriggerGroupMatcher();
            var jobKeys = await scheduler.GetTriggerKeys(matcher).ConfigureAwait(false);

            return jobKeys.Select(x => new KeyDto(x)).ToList();
        }

        [HttpGet]
        [Route("{triggerGroup}/{triggerName}/details")]
        public async Task<IActionResult> TriggerDetails(string schedulerName, string triggerGroup, string triggerName)
        {
            var scheduler = await GetScheduler(schedulerName).ConfigureAwait(false);
            var trigger = await scheduler.GetTrigger(new TriggerKey(triggerName, triggerGroup)).ConfigureAwait(false);

            if (trigger is null)
            {
                return NotFound();
            }
            
            var calendar = trigger.CalendarName != null
                ? await scheduler.GetCalendar(trigger.CalendarName).ConfigureAwait(false)
                : null;

            return Ok(TriggerDetailDto.Create(trigger, calendar));
        }

        [HttpPost]
        [Route("{triggerGroup}/{triggerName}/pause")]
        public async Task PauseTrigger(string schedulerName, string triggerGroup, string triggerName)
        {
            var scheduler = await GetScheduler(schedulerName).ConfigureAwait(false);
            await scheduler.PauseTrigger(new TriggerKey(triggerName, triggerGroup)).ConfigureAwait(false);
        }

        [HttpPost]
        [Route("pause")]
        public async Task PauseTriggers(string schedulerName, GroupMatcherDto groupMatcher)
        {
            var scheduler = await GetScheduler(schedulerName).ConfigureAwait(false);
            var matcher = (groupMatcher ?? new GroupMatcherDto()).GetTriggerGroupMatcher();
            await scheduler.PauseTriggers(matcher).ConfigureAwait(false);
        }

        [HttpPost]
        [Route("{triggerGroup}/{triggerName}/resume")]
        public async Task ResumeTrigger(string schedulerName, string triggerGroup, string triggerName)
        {
            var scheduler = await GetScheduler(schedulerName).ConfigureAwait(false);
            await scheduler.ResumeTrigger(new TriggerKey(triggerName, triggerGroup)).ConfigureAwait(false);
        }

        [HttpPost]
        [Route("resume")]
        public async Task ResumeTriggers(string schedulerName, GroupMatcherDto groupMatcher)
        {
            var scheduler = await GetScheduler(schedulerName).ConfigureAwait(false);
            var matcher = (groupMatcher ?? new GroupMatcherDto()).GetTriggerGroupMatcher();
            await scheduler.ResumeTriggers(matcher).ConfigureAwait(false);
        }

        private static async Task<IScheduler> GetScheduler(string schedulerName)
        {
            var scheduler = await SchedulerRepository.Instance.Lookup(schedulerName).ConfigureAwait(false);
            if (scheduler == null)
            {
                //throw new HttpResponseException(HttpStatusCode.NotFound);
                throw new KeyNotFoundException($"Scheduler {schedulerName} not found!");
            }
            return scheduler;
        }
    }
}