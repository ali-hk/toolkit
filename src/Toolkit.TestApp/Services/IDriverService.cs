﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Toolkit.TestApp.Models;

namespace Toolkit.TestApp.Services
{
    public interface IDriverService
    {
        IEnumerable<Driver> GetDrivers();
    }
}
