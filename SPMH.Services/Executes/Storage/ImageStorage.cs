using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SPMH.Services.Models;

namespace SPMH.Services.Executes.Storage
{
    public class ImageStorage
    {
        private static readonly HashSet<string> AllowedExt = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp"
        };

        private readonly string _physicalRoot;
        private readonly string _publicBasePath;


    }
}
