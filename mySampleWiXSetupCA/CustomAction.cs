﻿using Microsoft.Deployment.WindowsInstaller;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace mySampleWiXSetupCA
{
    public class CustomActions
    {
        [CustomAction]
        public static ActionResult CaRegisterAddIn(Session session)
        {
            bool bFoundOffice = false;

            try
            {
                session.Log("Enter try block of CaRegisterAddIn");

                string szOfficeRegKeyVersions = session["OFFICEREGKEYS_PROP"];
                string szXll32Bit = session["XLL32_PROP"];
                string szXll64Bit = session["XLL64_PROP"];
                string installFolder = session["INSTALLFOLDER"];

                session.Log(string.Format("szOfficeRegKeyVersions:{0};szXll32Bit:{1};szXll64Bit:{2};installFolder:{3}", szOfficeRegKeyVersions, szXll32Bit, szXll64Bit, installFolder));

                if (szOfficeRegKeyVersions.Length > 0)
                {
                    List<string> lstVersions = szOfficeRegKeyVersions.Split(',').ToList();

                    foreach (string szOfficeVersionKey in lstVersions)
                    {
                        double nVersion = double.Parse(szOfficeVersionKey, NumberStyles.Any, CultureInfo.InvariantCulture);

                        session.Log("Retrieving Registry Information for : " + Constants.SzBaseAddInKey + szOfficeVersionKey);

                        // get the OPEN keys from the Software\Microsoft\Office\[Version]\Excel\Options key, skip if office version not found.
                        if (Registry.CurrentUser.OpenSubKey(Constants.SzBaseAddInKey + szOfficeVersionKey, false) != null)
                        {
                            string szKeyName = Constants.SzBaseAddInKey + szOfficeVersionKey + @"\Excel\Options";

                            string szXllToRegister = GetAddInName(szXll32Bit, szXll64Bit, szOfficeVersionKey, nVersion);
                            //for a localmachine install the xll's should be in the installFolder
                            string fullPathToXll = Path.Combine(installFolder, szXllToRegister);

                            RegistryKey rkExcelXll = Registry.CurrentUser.OpenSubKey(szKeyName, true);
                            if (rkExcelXll != null)
                            {
                                session.Log("Success finding HKCU key for : " + szKeyName);
                                string[] szValueNames = rkExcelXll.GetValueNames();
                                bool bIsOpen = false;
                                int nMaxOpen = -1;

                                // check every value for OPEN keys
                                foreach (string szValueName in szValueNames)
                                {
                                    // if there are already OPEN keys, determine if our key is installed
                                    if (szValueName.StartsWith("OPEN"))
                                    {
                                        int nOpenVersion = int.TryParse(szValueName.Substring(4), out nOpenVersion) ? nOpenVersion : 0;
                                        int nNewOpen = szValueName == "OPEN" ? 0 : nOpenVersion;
                                        if (nNewOpen > nMaxOpen)
                                        {
                                            nMaxOpen = nNewOpen;
                                        }

                                        // if the key is our key, set the open flag
                                        if (rkExcelXll.GetValue(szValueName).ToString().Contains(szXllToRegister))
                                        {
                                            bIsOpen = true;
                                        }
                                    }
                                }

                                // if adding a new key
                                if (!bIsOpen)
                                {
                                    if (nMaxOpen == -1)
                                    {
                                        rkExcelXll.SetValue("OPEN", "/R \"" + fullPathToXll + "\"");
                                    }
                                    else
                                    {
                                        rkExcelXll.SetValue("OPEN" + (nMaxOpen + 1).ToString(CultureInfo.InvariantCulture), "/R \"" + fullPathToXll + "\"");
                                    }
                                    rkExcelXll.Close();
                                }
                                bFoundOffice = true;
                            }
                            else
                            {
                                session.Log("Unable to retrieve key for : " + szKeyName);
                            }
                        }
                        else
                        {
                            session.Log("Unable to retrieve registry Information for : " + Constants.SzBaseAddInKey + szOfficeVersionKey);
                        }
                    }
                }

                session.Log("End CaRegisterAddIn");
            }
            catch (System.Security.SecurityException ex)
            {
                session.Log("CaRegisterAddIn SecurityException" + ex.Message);
                bFoundOffice = false;
            }
            catch (UnauthorizedAccessException ex)
            {
                session.Log("CaRegisterAddIn UnauthorizedAccessException" + ex.Message);
                bFoundOffice = false;
            }
            catch (Exception ex)
            {
                session.Log("CaRegisterAddIn Exception" + ex.Message);
                bFoundOffice = false;
            }

            return bFoundOffice ? ActionResult.Success : ActionResult.Failure;
        }


        [CustomAction]
        public static ActionResult CaUnRegisterAddIn(Session session)
        {
            bool bFoundOffice = false;
            try
            {
                session.Log("Begin CaUnRegisterAddIn");

                string szOfficeRegKeyVersions = session["OFFICEREGKEYS_PROP"];
                string szXll32Bit = session["XLL32_PROP"];
                string szXll64Bit = session["XLL64_PROP"];
                string installFolder = session["INSTALLFOLDER"];

                session.Log(string.Format("szOfficeRegKeyVersions:{0};szXll32Bit:{1};szXll64Bit:{2};installFolder:{3}", szOfficeRegKeyVersions, szXll32Bit, szXll64Bit, installFolder));

                if (szOfficeRegKeyVersions.Length > 0)
                {
                    List<string> lstVersions = szOfficeRegKeyVersions.Split(',').ToList();

                    foreach (string szOfficeVersionKey in lstVersions)
                    {
                        double nVersion = double.Parse(szOfficeVersionKey, NumberStyles.Any, CultureInfo.InvariantCulture);
                        string szXllToUnRegister = GetAddInName(szXll32Bit, szXll64Bit, szOfficeVersionKey, nVersion);

                        // only remove keys where office version is found
                        if (Registry.CurrentUser.OpenSubKey(Constants.SzBaseAddInKey + szOfficeVersionKey, false) != null)
                        {
                            bFoundOffice = true;

                            string szKeyName = Constants.SzBaseAddInKey + szOfficeVersionKey + @"\Excel\Options";

                            RegistryKey rkAddInKey = Registry.CurrentUser.OpenSubKey(szKeyName, true);
                            if (rkAddInKey != null)
                            {
                                string[] szValueNames = rkAddInKey.GetValueNames();

                                foreach (string szValueName in szValueNames)
                                {
                                    if (szValueName.StartsWith("OPEN") && rkAddInKey.GetValue(szValueName).ToString().Contains(szXllToUnRegister))
                                    {
                                        rkAddInKey.DeleteValue(szValueName);
                                    }
                                }
                            }
                        }
                    }
                }

                session.Log("End CaUnRegisterAddIn");
            }
            catch (Exception ex)
            {
                session.Log(ex.Message);
            }

            return bFoundOffice ? ActionResult.Success : ActionResult.Failure;
        }


        //Using a registry key of outlook to determine the bitness of office may look like weird but that's the reality.
        //http://stackoverflow.com/questions/2203980/detect-whether-office-2010-is-32bit-or-64bit-via-the-registry
        public static string GetAddInName(string szXll32Name, string szXll64Name, string szOfficeVersionKey, double nVersion)
        {
            string szXllToRegister = string.Empty;

            if (nVersion >= 14)
            {
                // determine if office is 32-bit or 64-bit
                RegistryKey rkBitness = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Office\" + szOfficeVersionKey + @"\Outlook", false);
                if (rkBitness != null)
                {
                    object oBitValue = rkBitness.GetValue("Bitness");
                    if (oBitValue != null)
                    {
                        if (oBitValue.ToString() == "x64")
                        {
                            szXllToRegister = szXll64Name;
                        }
                        else
                        {
                            szXllToRegister = szXll32Name;
                        }
                    }
                    else
                    {
                        szXllToRegister = szXll32Name;
                    }
                }
            }
            else
            {
                szXllToRegister = szXll32Name;
            }

            return szXllToRegister;
        }

    }
}
