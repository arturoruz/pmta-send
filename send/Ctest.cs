﻿using NLog;
using port25.pmta.api.submitter;
using send.helpers;
using System;
using System.Collections.Generic;

namespace send
{
    class Ctest
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        public Message Message { get; set; }
        public string Return_path { get; set; }
        public string[] Emails { get; set; }
        public string Header { get; set; }
        public string Body { get; set; }
        public string Mta { get; set; }
        public string Username { get; set; }
        public string Redirect { get; set; }
        public string Unsubscribe { get; set; }
        public string Platform { get; set; }
        public dynamic Servers { get; set; }

        public int Id { get; set; }

        public Ctest(dynamic data)
        {
            this.Id = data.id ?? 0;
            this.Return_path = !string.IsNullOrWhiteSpace((string)data.return_path) ? (string)data.return_path : "[rnd]@[domain]";
            this.Emails = data.test_emails.ToObject<string[]>() ?? throw new ArgumentNullException(nameof(data.emails));
            this.Header = Text.Base64Decode(Convert.ToString(data.header)) ?? throw new ArgumentNullException(nameof(data.header));
            this.Body = Text.Base64Decode(Convert.ToString(data.body)) ?? "";
            this.Mta = data.mta ?? throw new ArgumentNullException(nameof(data.mta));
            this.Username = data.username ?? throw new ArgumentNullException(nameof(data.username));
            this.Servers = data.servers ?? throw new ArgumentNullException(nameof(data.servers));
            this.Redirect = data.redirect ?? throw new ArgumentNullException(nameof(data.redirect));
            this.Unsubscribe = data.unsubscribe ?? throw new ArgumentNullException(nameof(data.unsubscribe));
            this.Platform = data.platform ?? throw new ArgumentNullException(nameof(data.platform));
        }

        public List<string> Send()
        {
            List<string> data = new List<string>();
            Encryption enc = new Encryption();
            foreach (dynamic server in Servers)
            {
                try
                {
                    Pmta p = new Pmta((string)server.mainip, (string)server.password, (string)server.username, (int)server.port);
                    foreach (dynamic ip in server.ips)
                    {
                        string email_ip = ip.ip;
                        string domain = ip.domain;
                        string rdns = Text.rdns(email_ip, domain);
                        string vmta_ip = email_ip.Replace(':', '.');
                        string vmta = Mta.ToLower() == "none" ? $"mta-{vmta_ip}" : (Mta == "vmta" ? $"vmta-{vmta_ip}-{Username}" : $"smtp-{vmta_ip}-{Username}");
                        string job = $"0_CAMPAIGN-TEST_{Id}_{Username}";

                        string redirect = enc.encrypt($"r!!{Id}!!{ip.idip}!!{ip.idddomain}!!0!!{Redirect}!!{Platform}"); //r_idc_idi_idd_ide_link_platform
                        string unsubscribe = enc.encrypt($"u!!{Id}!!{ip.idip}!!{ip.idddomain}!!0!!{Unsubscribe}"); //u_idc_idi_idd_ide_link
                        string open = enc.encrypt($"o!!{Id}!!{ip.idip}!!{ip.idddomain}!!0"); ;//o_idc_idi_idd_ide
                        string optout = enc.encrypt($"out!!{new Random().Next(5, 15)}"); // out_random

                        foreach (string email in Emails)
                        {
                            string boundary = Text.random("[rndlu/30]");
                            string emailName = email.Split('@')[0];
                            string rp = Text.Build_rp(Return_path, domain, rdns, emailName);
                            string hd = Text.Build_header(Header, email_ip, domain, rdns, email, emailName, boundary);
                            string bd = Text.Build_body(Body, email_ip, domain, rdns, email, emailName, boundary);
                            
                            bd = Text.generate_links(bd, redirect, unsubscribe, open, optout);
                            Message = new Message(rp);
                            Message.AddData(hd + "\n" + bd);
                            Message.AddRecipient(new Recipient(email));
                            Message.VirtualMTA = vmta;
                            Message.JobID = job;
                            Message.EnvID = $"{job}_{DateTime.Now.ToString("dd-MM-yyyy_HH-mm-ss_")}";
                            Message.Verp = false;
                            Message.Encoding = Encoding.EightBit;
                            p.Send(Message);
                        }
                    }
                    data.Add($"SERVER {server.mainip} OK");
                    p.Close();
                }
                catch (Exception ex)
                {
                    data.Add($"ERROR SERVER {server.mainip} - {ex.Message}");
                    logger.Error(ex.Message);
                }
            }
            return data;
        }

    }
}