﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using SiteServer.CMS.DataCache;
using SiteServer.CMS.Model;
using SiteServer.CMS.Model.Attributes;
using SiteServer.Utils;
using SiteServer.Utils.Enumerations;

namespace SiteServer.CMS.Core
{
    public static class SystemManager
    {
        static SystemManager()
        {
            try
            {
                ProductVersion = FileVersionInfo.GetVersionInfo(PathUtils.GetBinDirectoryPath("SiteServer.CMS.dll")).ProductVersion;
                PluginVersion = FileVersionInfo.GetVersionInfo(PathUtils.GetBinDirectoryPath("SiteServer.Plugin.dll")).ProductVersion;

                if (Assembly.GetExecutingAssembly()
                    .GetCustomAttributes(typeof(TargetFrameworkAttribute), false)
                    .SingleOrDefault() is TargetFrameworkAttribute targetFrameworkAttribute)
                {
                    TargetFramework = targetFrameworkAttribute.FrameworkName;
                }

                EnvironmentVersion = Environment.Version.ToString();

                //DotNetVersion = FileVersionInfo.GetVersionInfo(typeof(Uri).Assembly.Location).ProductVersion;
            }
            catch (Exception _err)
            {
                Console.WriteLine(_err);
            }

            //var ssemblyName = assembly.GetName();
            //var assemblyVersion = ssemblyName.Version;
            //var version = assemblyVersion.ToString();
            //if (StringUtils.EndsWith(version, ".0"))
            //{
            //    version = version.Substring(0, version.DataLength - 2);
            //}
            //Version = version;
        }

        public static string ProductVersion { get; }

        public static string PluginVersion { get; }

        public static string TargetFramework { get; }

        public static string EnvironmentVersion { get; }
        /// <summary>
        /// 系统是否已经安装
        /// </summary>
        public static Boolean IsInstalled { get; private set; } = false;
        /// <summary>
        /// 域名和站点信息对应Map
        /// </summary>
        public static SortedList<String,SiteInfo> SiteList { get; private set; } = new SortedList<string, SiteInfo>();
        /// <summary>
        /// 所有站点文件夹名称连接字符串，以|开始和结尾，多个文件夹之间使用|分割
        /// </summary>
        public static String SiteDirs { get; private set; } = String.Empty;

        public static void InstallDatabase(string adminName, string adminPassword)
        {
            SyncDatabase();

            if (!string.IsNullOrEmpty(adminName) && !string.IsNullOrEmpty(adminPassword))
            {
                var administratorInfo = new AdministratorInfo
                {
                    UserName = adminName,
                    Password = adminPassword
                };

                DataProvider.AdministratorDao.Insert(administratorInfo, out _);
                DataProvider.AdministratorsInRolesDao.AddUserToRole(adminName, EPredefinedRoleUtils.GetValue(EPredefinedRole.ConsoleAdministrator));
            }
        }

        public static void CreateSiteServerTables()
        {
            foreach (var provider in DataProvider.AllProviders)
            {
                if (string.IsNullOrEmpty(provider.TableName) || provider.TableColumns == null || provider.TableColumns.Count <= 0) continue;

                if (!DataProvider.DatabaseDao.IsTableExists(provider.TableName))
                {
                    DataProvider.DatabaseDao.CreateTable(provider.TableName, provider.TableColumns, out _, out _);
                }
                else
                {
                    DataProvider.DatabaseDao.AlterSystemTable(provider.TableName, provider.TableColumns);
                }
            }
        }

        public static void SyncContentTables()
        {
            var tableNameList = SiteManager.GetAllTableNameList();
            foreach (var tableName in tableNameList)
            {
                if (!DataProvider.DatabaseDao.IsTableExists(tableName))
                {
                    DataProvider.DatabaseDao.CreateTable(tableName, DataProvider.ContentDao.TableColumns, out _, out _);
                }
                else
                {
                    DataProvider.DatabaseDao.AlterSystemTable(tableName, DataProvider.ContentDao.TableColumns, ContentAttribute.DropAttributes.Value);
                }
            }
        }

        public static void UpdateConfigVersion()
        {
            var configInfo = DataProvider.ConfigDao.GetConfigInfo();
            if (configInfo == null)
            {
                configInfo = new ConfigInfo(0, true, ProductVersion, DateTime.Now, string.Empty);
                DataProvider.ConfigDao.Insert(configInfo);
            }
            else
            {
                configInfo.DatabaseVersion = ProductVersion;
                configInfo.IsInitialized = true;
                configInfo.UpdateDate = DateTime.Now;
                DataProvider.ConfigDao.Update(configInfo);
            }
        }

        public static void SyncDatabase()
        {
            CacheUtils.ClearAll();

            CreateSiteServerTables();

            SyncContentTables();

            UpdateConfigVersion();
        }


        public static bool IsNeedUpdate()
        {
            return !StringUtils.EqualsIgnoreCase(ProductVersion, DataProvider.ConfigDao.GetDatabaseVersion());
        }
        /// <summary>
        /// 检查系统是否已经安装
        /// </summary>
        /// <returns>系统是否已经安装</returns>
        public static bool CheckIsInstalled()
        {
            if (!IsInstalled)
            {
                IsInstalled = DataProvider.ConfigDao.IsInitialized();
            }
            if (IsInstalled) {
                UpdateSites();
            }
            return SystemManager.IsInstalled;
        }

        public static void UpdateSites()
        {
            SortedList<String, SiteInfo> sites = new SortedList<string, SiteInfo>();
            StringBuilder siteDirs = new StringBuilder("|");
            if (IsInstalled) {
                List<SiteInfo> siteList = DataProvider.SiteDao.GetAllSiteList();
                foreach (SiteInfo site in siteList) {
                    siteDirs.Append(site.SiteDir).Append("|");
                    if (site.IsRoot)
                    {
                        sites.Add("", site);
                    }
                    else
                    {
                        String[] domainNames = site.DomainName.Split(';');
                        foreach (String domainName in domainNames) {
                            if (!sites.ContainsKey(domainName))
                            {
                                sites.Add(domainName, site);
                            }
                        }
                    }
                }
            }
            SiteDirs = siteDirs.ToString();
            SiteList = sites;
        }

        //public static bool DetermineRedirectToInstaller()
        //{
        //    if (!IsNeedInstall()) return false;
        //    PageUtils.Redirect(PageUtils.GetAdminDirectoryUrl("Installer"));
        //    return true;
        //}
    }
}
