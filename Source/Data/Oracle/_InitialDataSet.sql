INSERT INTO Protocol(Acronym, Name, Type, Category, AssemblyName, TypeName, LoadOrder) VALUES('Downloader', 'Remote Downloader', 'File', 'File', 'openMIC.exe', 'openMIC.Downloader', 12);
INSERT INTO Protocol(Acronym, Name, Type, Category, AssemblyName, TypeName, LoadOrder) VALUES('Modbus', 'Modbus Poller', 'Measurement', 'Device', 'openMIC.exe', 'openMIC.ModbusPoller', 13);

INSERT INTO Statistic(Source, SignalIndex, NAME, Description, AssemblyName, TypeName, MethodName, Arguments, Enabled, DataType, DisplayFormat, IsConnectedState, LoadOrder) VALUES('Downloader', 1, 'Downloader Enabled', 'Boolean value representing if the downloader was continually enabled with access to local download path during last reporting interval.', 'openMIC.exe', 'openMIC.Downloader', 'GetDownloaderStatistic_Enabled', '', 1, 'System.Boolean', '{0}', 1, 1);
INSERT INTO Statistic(Source, SignalIndex, NAME, Description, AssemblyName, TypeName, MethodName, Arguments, Enabled, DataType, DisplayFormat, IsConnectedState, LoadOrder) VALUES('Downloader', 2, 'Downloader Attempted Connections', 'Number of attempted FTP connections reported by the downloader during last reporting interval.', 'openMIC.exe', 'openMIC.Downloader', 'GetDownloaderStatistic_AttemptedConnections', '', 1, 'System.Int64', '{0:N0}', 0, 2);
INSERT INTO Statistic(Source, SignalIndex, NAME, Description, AssemblyName, TypeName, MethodName, Arguments, Enabled, DataType, DisplayFormat, IsConnectedState, LoadOrder) VALUES('Downloader', 3, 'Downloader Successful Connections', 'Number of successful FTP connections reported by the downloader during last reporting interval.', 'openMIC.exe', 'openMIC.Downloader', 'GetDownloaderStatistic_SuccessfulConnections', '', 1, 'System.Int64', '{0:N0}', 0, 3);
INSERT INTO Statistic(Source, SignalIndex, NAME, Description, AssemblyName, TypeName, MethodName, Arguments, Enabled, DataType, DisplayFormat, IsConnectedState, LoadOrder) VALUES('Downloader', 4, 'Downloader Failed Connections', 'Number of failed FTP connections reported by the downloader during last reporting interval.', 'openMIC.exe', 'openMIC.Downloader', 'GetDownloaderStatistic_FailedConnections', '', 1, 'System.Int64', '{0:N0}', 0, 4);
INSERT INTO Statistic(Source, SignalIndex, NAME, Description, AssemblyName, TypeName, MethodName, Arguments, Enabled, DataType, DisplayFormat, IsConnectedState, LoadOrder) VALUES('Downloader', 5, 'Downloader Attempted Dial-ups', 'Number of attempted dial-ups reported by the downloader during last reporting interval.', 'openMIC.exe', 'openMIC.Downloader', 'GetDownloaderStatistic_AttemptedDialUps', '', 1, 'System.Int64', '{0:N0}', 0, 5);
INSERT INTO Statistic(Source, SignalIndex, NAME, Description, AssemblyName, TypeName, MethodName, Arguments, Enabled, DataType, DisplayFormat, IsConnectedState, LoadOrder) VALUES('Downloader', 6, 'Downloader Successful Dial-ups', 'Number of successful dial-ups reported by the downloader during last reporting interval.', 'openMIC.exe', 'openMIC.Downloader', 'GetDownloaderStatistic_SuccessfulDialUps', '', 1, 'System.Int64', '{0:N0}', 0, 6);
INSERT INTO Statistic(Source, SignalIndex, NAME, Description, AssemblyName, TypeName, MethodName, Arguments, Enabled, DataType, DisplayFormat, IsConnectedState, LoadOrder) VALUES('Downloader', 7, 'Downloader Failed Dial-ups', 'Number of failed dial-ups reported by the downloader during last reporting interval.', 'openMIC.exe', 'openMIC.Downloader', 'GetDownloaderStatistic_FailedDialUps', '', 1, 'System.Int64', '{0:N0}', 0, 7);
INSERT INTO Statistic(Source, SignalIndex, NAME, Description, AssemblyName, TypeName, MethodName, Arguments, Enabled, DataType, DisplayFormat, IsConnectedState, LoadOrder) VALUES('Downloader', 8, 'Downloader Files Downloaded', 'Number of downloaded files reported by the downloader during last reporting interval.', 'openMIC.exe', 'openMIC.Downloader', 'GetDownloaderStatistic_FilesDownloaded', '', 1, 'System.Int64', '{0:N0}', 0, 8);
INSERT INTO Statistic(Source, SignalIndex, NAME, Description, AssemblyName, TypeName, MethodName, Arguments, Enabled, DataType, DisplayFormat, IsConnectedState, LoadOrder) VALUES('Downloader', 9, 'Downloader MegaBytes Downloaded', 'Number of downloaded megabytes reported by the downloader during last reporting interval.', 'openMIC.exe', 'openMIC.Downloader', 'GetDownloaderStatistic_MegaBytesDownloaded', '', 1, 'System.Double', '{0:N3} MB', 0, 9);
INSERT INTO Statistic(Source, SignalIndex, NAME, Description, AssemblyName, TypeName, MethodName, Arguments, Enabled, DataType, DisplayFormat, IsConnectedState, LoadOrder) VALUES('Downloader', 10, 'Downloader Connected Time', 'Total FTP connection time reported by the downloader during last reporting interval.', 'openMIC.exe', 'openMIC.Downloader', 'GetDownloaderStatistic_TotalConnectedTime', '', 1, 'System.Double', '{0:N3} Seconds', 0, 10);
INSERT INTO Statistic(Source, SignalIndex, NAME, Description, AssemblyName, TypeName, MethodName, Arguments, Enabled, DataType, DisplayFormat, IsConnectedState, LoadOrder) VALUES('Downloader', 11, 'Downloader Dial-up Time', 'Total dial-up time reported by the downloader during last reporting interval.', 'openMIC.exe', 'openMIC.Downloader', 'GetDownloaderStatistic_TotalDialUpTime', '', 1, 'System.Double', '{0:N3} Seconds', 0, 11);

DELETE FROM VendorDevice;
DELETE FROM Vendor;

ALTER SEQUENCE SEQ_VendorDevice INCREMENT BY -19;
SELECT SEQ_VendorDevice.nextval FROM dual;
ALTER SEQUENCE SEQ_VendorDevice INCREMENT BY 1 MINVALUE 0;

ALTER SEQUENCE SEQ_Vendor INCREMENT BY -15;
SELECT SEQ_Vendor.nextval FROM dual;
ALTER SEQUENCE SEQ_Vendor INCREMENT BY 1 MINVALUE 0;

INSERT INTO Vendor(Acronym, Name, PhoneNumber, ContactEmail, URL) VALUES('OTHER', 'Other / Unspecified', '', '', '');
INSERT INTO Vendor(Acronym, Name, PhoneNumber, ContactEmail, URL) VALUES('APP', 'APP Engineering Inc.', '', '', 'http://appengineering.com/');
INSERT INTO Vendor(Acronym, Name, PhoneNumber, ContactEmail, URL) VALUES('EMAX', 'E-MAX Instruments, Inc.', '', '', 'http://www.e-maxinstruments.com/');
INSERT INTO Vendor(Acronym, Name, PhoneNumber, ContactEmail, URL) VALUES('QUAL', 'Qualitrol LLC', '', '', 'http://www.qualitrolcorp.com/');
INSERT INTO Vendor(Acronym, Name, PhoneNumber, ContactEmail, URL) VALUES('IGRID', 'I-Grid - Rockwell Automation, Inc.', '', '', 'https://www.igrid.com/');

INSERT INTO VendorDevice(VendorID, Name, Description, URL) VALUES(2, 'APP-501', 'APP-501 Multifunction Recorder', 'http://appengineering.com/products/601_Sales_Literature_Rev_4.pdf');
INSERT INTO VendorDevice(VendorID, Name, Description, URL) VALUES(2, 'APP-601', 'APP-601 Multifunction Recorder', 'http://appengineering.com/products/601_Sales_Literature_Rev_4.pdf');
INSERT INTO VendorDevice(VendorID, Name, Description, URL) VALUES(3, 'EMAX-DII', 'E-MAX Director DII DFR', 'http://www.e-maxinstruments.com/images/pdf/product-catalog/E-MAX-Digital-Fault-Recorder-DII-2014.pdf');
INSERT INTO VendorDevice(VendorID, Name, Description, URL) VALUES(3, 'EMAX-SUR', 'E-MAX Surveyor Distributed DFR', 'http://www.e-maxinstruments.com/images/pdf/product-catalog/E-MAX-Surveyor-Distributed-DFR-2015.pdf');
INSERT INTO VendorDevice(VendorID, Name, Description, URL) VALUES(3, 'EMAX-WAV', 'E-MAX Wave DFR', 'http://www.e-maxinstruments.com/images/pdf/product-catalog/E-MAX-DFR-Wave-2014%20.pdf');
INSERT INTO VendorDevice(VendorID, Name, Description, URL) VALUES(4, 'BEN-5000', 'BEN 5000 Portable DFR', 'http://www.qualitrolcorp.com/Products/BEN_5000_to_BEN_6000_Upgrade/');
INSERT INTO VendorDevice(VendorID, Name, Description, URL) VALUES(4, 'BEN-6000', 'BEN 6000 Portable DFR', 'http://www.qualitrolcorp.com/Products/BEN_6000_Portable_digital_fault_recorder_(with_options_for_power_quality_and_PMU)/');
INSERT INTO VendorDevice(VendorID, Name, Description, URL) VALUES(5, 'ISENSE', 'I-Sense Monitor', 'https://www.igrid.com/igrid/SupportV3.jsp');