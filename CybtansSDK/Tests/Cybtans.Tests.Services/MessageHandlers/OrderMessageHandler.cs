﻿using AutoMapper.Mappers;
using Cybtans.Entities;
using Cybtans.Entities.EventLog;
using Cybtans.Test.Domain;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Cybtans.Tests.Services
{

    public class OrderMessageHandler : IEntityEventsHandler<Order>
    {
        EntityEventDelegateHandler<OrderMessageHandler> _options;

        public OrderMessageHandler(EntityEventDelegateHandler<OrderMessageHandler> options)
        {
            _options = options;
        }

        public Task HandleMessage(EntityCreated<Order> message)
        {
            _options.OnCreated?.Invoke(this, message);
            return Task.CompletedTask;
        }

        public Task HandleMessage(EntityUpdated<Order> message)
        {
            _options.OnUpdated?.Invoke(this, message);
            return Task.CompletedTask;
        }

        public Task HandleMessage(EntityDeleted<Order> message)
        {
            _options.OnDeleted?.Invoke(this, message);
            return Task.CompletedTask;
        }
    }
}