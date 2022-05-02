using System;
using System.Collections.Generic;
using System.Linq;

namespace Antiban
{
    public class Antiban
    {
        private readonly List<AntibanResult> _results = new();
        private readonly Queue<EventMessage> _registredMesages = new();
        private readonly Queue<Func<List<AntibanResult>, List<EventMessage>, EventMessage, TimeSpan>> _sendTimeCalculators = new();

        public Antiban()
        {
            _sendTimeCalculators.Enqueue(SentTimeCalculators.NotLess24HourForSameNumbersAndPriority1);
            _sendTimeCalculators.Enqueue(SentTimeCalculators.NotLessMinuteForSameNumbers);
            _sendTimeCalculators.Enqueue(SentTimeCalculators.NotLess10SecondsForAnyMessage);
        }

        public void AddBusinesRuleApplicator(Func<List<AntibanResult>, List<EventMessage>, EventMessage, TimeSpan> rule)
            => _sendTimeCalculators.Enqueue(rule);

        /// <summary>
        /// Добавление сообщений в систему, для обработки порядка сообщений
        /// </summary>
        /// <param name="eventMessage"></param>
        public void PushEventMessage(EventMessage eventMessage)
        {
            _registredMesages.Enqueue(eventMessage);
        }

        /// <summary>
        /// Вовзращает порядок отправок сообщений
        /// </summary>
        /// <returns></returns>
        public List<AntibanResult> GetResult()
        {
            _results.Clear();
            foreach (var eventMessage in _registredMesages)
            {
                var addToSentTime = _sendTimeCalculators.Any() ?
                _sendTimeCalculators
                .Where(x => x is not null)
                    .Select(t => t(_results, _registredMesages.ToList(), eventMessage))
                    .Max()
                : TimeSpan.FromSeconds(0);

                _results.Add(new AntibanResult()
                {
                    EventMessageId = eventMessage.Id,
                    SentDateTime = eventMessage.DateTime.Add(addToSentTime),
                });
            }

            return _results.OrderBy(x => x.SentDateTime).ToList();
        }
    }

    public abstract class SentTimeCalculators
    {
        private static TimeSpan AbsoluteDifference(DateTime start, DateTime end)
        {
            return (start - end).Duration();
        }

        public static TimeSpan NotLess10SecondsForAnyMessage(List<AntibanResult> results,
                                                             List<EventMessage> registredMessages,
                                                             EventMessage messageToAdd)
        {
            if (messageToAdd == null)
            {
                throw new ArgumentNullException();
            }

            if (results == null)
            {
                throw new ArgumentNullException();
            }

            var result = new TimeSpan();

            var messages = registredMessages
                .Join(
                results,
                registred => registred.Id,
                result => result.EventMessageId,
                (registred, result) =>
                new
                {
                    MessageId = registred.Id,
                    RegistrationTime = registred.DateTime,
                    SentTime = result.SentDateTime,
                    Phone = registred.Phone,
                    Priority = registred.Priority,
                })
                .ToList();

            var within10Seconds = messages
                    .Where(x => x.SentTime >= messageToAdd.DateTime.AddSeconds(-10)
                         && x.SentTime <= messageToAdd.DateTime.AddSeconds(10));

            if (within10Seconds.Any())
            {
                var lastMessage = within10Seconds.MaxBy(x => x.SentTime);

                return AbsoluteDifference(lastMessage.SentTime.AddSeconds(10), messageToAdd.DateTime);
            }

            return result;
        }

        public static TimeSpan NotLessMinuteForSameNumbers(List<AntibanResult> results,
                                                     List<EventMessage> registredMessages,
                                                     EventMessage messageToAdd)
        {
            if (messageToAdd == null)
            {
                throw new ArgumentNullException();
            }

            if (registredMessages == null)
            {
                throw new ArgumentNullException();
            }

            if (results == null)
            {
                throw new ArgumentNullException();
            }

            var messages = registredMessages
                .Where(x => x.Phone == messageToAdd.Phone)
                .Join(
                results,
                registred => registred.Id,
                result => result.EventMessageId,
                (registred, result) =>
                new
                {
                    MessageId = registred.Id,
                    RegistrationTime = registred.DateTime,
                    SentTime = result.SentDateTime,
                    Phone = registred.Phone,
                    Priority = registred.Priority,
                })
                .ToList();

            var withinOneMinute = messages
                .Where(x => x.SentTime >= messageToAdd.DateTime.AddMinutes(-1)
                         && x.SentTime <= messageToAdd.DateTime);

            if (withinOneMinute.Any())
            {
                var lastResult = withinOneMinute
                    .MaxBy(r => r.SentTime);

                var difference = AbsoluteDifference(messageToAdd.DateTime, lastResult.SentTime);
                if (difference.TotalMinutes <= 1)
                {
                    var result = TimeSpan.FromMinutes(1) - difference;
                    return result;
                }
            }

            return new TimeSpan();
        }

        public static TimeSpan NotLess24HourForSameNumbersAndPriority1(List<AntibanResult> results,
                                                                       List<EventMessage> registredMessages,
                                                                       EventMessage messageToAdd)
        {
            if (messageToAdd == null)
            {
                throw new ArgumentNullException();
            }

            if (registredMessages == null)
            {
                throw new ArgumentNullException();
            }

            if (results == null)
            {
                throw new ArgumentNullException();
            }

            var result = new TimeSpan();

            if (messageToAdd.Priority == 1)
            {
                var lastMessagesIds = registredMessages
                .Where(x => x.Phone == messageToAdd.Phone && x.Priority == 1)
                .Join(
                results,
                registred => registred.Id,
                result => result.EventMessageId,
                (registred, result) =>
                new
                {
                    MessageId = registred.Id,
                    RegistrationTime = registred.DateTime,
                    SentTime = result.SentDateTime,
                    Phone = registred.Phone,
                    Priority = registred.Priority,
                })
                .ToList();

                var within24Hours = lastMessagesIds
                    .Where(x => x.SentTime >= messageToAdd.DateTime.AddHours(-24));

                if (within24Hours.Any())
                {
                    var lastResult = within24Hours
                        .MaxBy(r => r.SentTime);

                    return AbsoluteDifference(lastResult.SentTime.AddHours(24), messageToAdd.DateTime);
                }
            }

            return result;
        }
    }
}