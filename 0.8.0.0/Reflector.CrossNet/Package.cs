using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Reflector;
using Reflector.CodeModel;
using CrossNet.Net;

namespace Reflector.CrossNet
{
    public class Package : IPackage
    {
        public void Load(IServiceProvider serviceProvider)
        {
            mServiceProvider = serviceProvider;
            mWindowManager = (IWindowManager)serviceProvider.GetService(typeof(IWindowManager));
            mWindowManager.Load += new EventHandler(WindowManager_Load);
        }

        public void Unload()
        {
            mWindowManager.Load -= new EventHandler(WindowManager_Load);
        }

        private void WindowManager_Load(object sender, EventArgs e)
        {
            string[] commandLineArguments = Environment.GetCommandLineArgs();

            bool canClose = false;
            foreach (string argument in commandLineArguments)
            {
                string lowerArgument = argument.ToLower();
                if (lowerArgument.StartsWith(ARG) == false)
                {
                    continue;
                }
                string fileName = lowerArgument.Substring(ARG.Length);
                // The command line has been recognized, we will close at the end...
                canClose = true;

                if (File.Exists(fileName) == false)
                {
                    // We should display a message box here!
                    continue;
                }

                Provider.Initialize(mServiceProvider);
                bool noError = Provider.GenerateCodeFromConfigFile(fileName);
                if (noError == false)
                {
                    // We should display a message box here
                    // let's not continue further...
                    break;
                }
            }

            if (canClose)
            {
                mWindowManager.Close();
            }
        }

        private IServiceProvider mServiceProvider;
        private IWindowManager mWindowManager;

        private const string ARG = "/crossnet:";
    }
}
