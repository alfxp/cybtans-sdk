﻿using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Cybtans.AspNetCore.Migrations
{
    public interface IDbContextSeed<TContext>
        where TContext :DbContext
    {
        Task Seed(TContext dbContext);
    }
}
