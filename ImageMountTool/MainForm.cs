using System.ComponentModel;
using System.Diagnostics;
using ImageMounter;
using ImageMounter.DevIo.Server.Interaction;
using ImageMounter.Interop.IO;
using ImageMounter.IO;
using ImageMounter.IO.Native.Enum;
using ImageMounter.PSDisk;
using Server.Interaction;

namespace ImageMountTool
{
    public partial class MainForm : Form
    {


        private ScsiAdapter Adapter;
        private readonly List<ServiceListItem> ServiceList = new List<ServiceListItem>();

        private bool IsClosing;
        private uint? LastCreatedDevice;

        private readonly AutoResetEvent DeviceListRefreshEvent = new AutoResetEvent(initialState: false);


        public MainForm()
        {
            InitializeComponent();
        }


        private void btnRefresh_Click(object sender, EventArgs e)
        {
            if (IsClosing || Disposing || IsDisposed)
            {
                return;
            }

            if (InvokeRequired)
            {
                Invoke(new Action(RefreshDeviceList));
                return;
            }

            SetLabelBusy();

            Thread.Sleep(400);

            btnRemoveSelected.Enabled = false;

            //DeviceListRefreshEvent.Set(;
        }


        protected override void OnClosing(CancelEventArgs e)
        {
            IsClosing = true;

            try
            {
                ICollection<ServiceListItem> ServiceItems;
                lock (ServiceList)
                    ServiceItems = ServiceList.ToArray();
                foreach (var Item in ServiceItems)
                {
                    if (Item?.Service?.HasDiskDevice)
                    {
                        Trace.WriteLine(
                            $"Requesting service for device {Item.Service.DiskDeviceNumber} to shut down...");
                        Item.Service.DismountAndStopServiceThread(TimeSpan.FromSeconds(10));
                    }
                    else
                        ServiceList.Remove(Item);
                }
            }
            catch (Exception ex)
            {
                e.Cancel = true;
                Trace.WriteLine(ex.ToString());
                MessageBox.Show(this, ex.JoinMessages(), ex.GetBaseException().GetType().Name, MessageBoxButtons.OK,
                    MessageBoxIcon.Exclamation);
            }

            if (e.Cancel)
            {
                IsClosing = false;
                RefreshDeviceList();
                return;
            }

            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            IsClosing = true;

            DeviceListRefreshEvent.Set();

            base.OnClosed(e);
        }

        private void RefreshDeviceList()
        {
            if (IsClosing || Disposing || IsDisposed)
                return;

            if (InvokeRequired)
            {
                Invoke(new Action(RefreshDeviceList));
                return;
            }

            SetLabelBusy();

            Thread.Sleep(400);

            btnRemoveSelected.Enabled = false;

            DeviceListRefreshEvent.Set();
        }

        private void SetLabelBusy()
        {
            {
                var withBlock = lblDeviceList;
                withBlock.Text = "Loading device list...";
                withBlock.ForeColor = Color.White;
                withBlock.BackColor = Color.DarkRed;
            }

            lblDeviceList.Update();
        }

        private void SetDiskView(List<DiskStateView> list, bool finished)
        {
            if (finished)
            {
                {
                    var withBlock = lblDeviceList;
                    withBlock.Text = "Device list";
                    withBlock.ForeColor = SystemColors.ControlText;
                    withBlock.BackColor = SystemColors.Control;
                }
            }

            foreach (var item in from view in list
                     join serviceItem in ServiceList on view.ScsiId equals
                         serviceItem.Service.DiskDeviceNumber.ToString("X6")
                     select view)

                item.view.DeviceProperties.Filename = item.serviceItem.ImageFile;

            foreach (var prop in from item in list
                     where item.DeviceProperties.Filename == null
                     select item.DeviceProperties)

                prop.Filename = "RAM disk";

            DiskStateViewBindingSource.DataSource = list;

            if (list == null || list.Count == 0)
            {
                btnRemoveSelected.Enabled = false;
                btnRemoveAll.Enabled = false;
                return;
            }

            btnRemoveAll.Enabled = true;

            if (LastCreatedDevice.HasValue)
            {
                ; /* Cannot convert LocalDeclarationStatementSyntax, System.InvalidOperationException: Sequence contains no elements
   at System.Linq.Enumerable.Single[TSource](IEnumerable`1 source)
   at ICSharpCode.CodeConverter.CSharp.VisualBasicConverter.NodesVisitor.VisitQueryExpression(QueryExpressionSyntax node)
   at Microsoft.CodeAnalysis.VisualBasic.Syntax.QueryExpressionSyntax.Accept[TResult](VisualBasicSyntaxVisitor`1 visitor)
   at Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxVisitor`1.Visit(SyntaxNode node)
   at ICSharpCode.CodeConverter.CSharp.CommentConvertingNodesVisitor.DefaultVisit(SyntaxNode node)
   at Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxVisitor`1.VisitQueryExpression(QueryExpressionSyntax node)
   at Microsoft.CodeAnalysis.VisualBasic.Syntax.QueryExpressionSyntax.Accept[TResult](VisualBasicSyntaxVisitor`1 visitor)
   at ICSharpCode.CodeConverter.CSharp.CommonConversions.ConvertInitializer(VariableDeclaratorSyntax declarator)
   at ICSharpCode.CodeConverter.CSharp.CommonConversions.SplitVariableDeclarations(VariableDeclaratorSyntax declarator)
   at ICSharpCode.CodeConverter.CSharp.VisualBasicConverter.MethodBodyVisitor.VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
   at Microsoft.CodeAnalysis.VisualBasic.Syntax.LocalDeclarationStatementSyntax.Accept[TResult](VisualBasicSyntaxVisitor`1 visitor)
   at Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxVisitor`1.Visit(SyntaxNode node)
   at ICSharpCode.CodeConverter.CSharp.CommentConvertingMethodBodyVisitor.ConvertWithTrivia(SyntaxNode node)
   at ICSharpCode.CodeConverter.CSharp.CommentConvertingMethodBodyVisitor.DefaultVisit(SyntaxNode node)

Input: 
            Dim obj =
                Aggregate diskview In list
                Into FirstOrDefault(diskview.DeviceProperties.DeviceNumber = LastCreatedDevice.Value)

 */
                LastCreatedDevice = default(UInteger?);

                // ' If a refresh started before device was added and has not yet finished,
                // ' the newly created device will not be found here. This routine will be
                // ' called again when next refresh has finished in which case an object
                // ' will be found.
                if (obj == null)
                    return;

                if (obj.IsOffline.GetValueOrDefault())
                {
                    if (MessageBox.Show(this,
                            "The new virtual disk was mounted in offline mode. Do you wish to bring the virtual disk online?",
                            "Disk offline", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.Yes)
                    {
                        try
                        {
                            Update();

                            if (obj.DevicePath.StartsWith(@"\\?\PhysicalDrive", StringComparison.Ordinal))
                            {
                                using (new AsyncMessageBox("Please wait..."))
                                using (DiskDevice device = new DiskDevice(obj.DevicePath, FileAccess.ReadWrite))
                                {
                                    device.DiskPolicyOffline = false;
                                }
                            }

                            MessageBox.Show(this, "The virtual disk was successfully brought online.", "Disk online",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        catch (Exception ex)
                        {
                            Trace.WriteLine(ex.ToString());
                            MessageBox.Show(this, $"An error occurred: {ex.JoinMessages()}",
                                ex.GetBaseException().GetType().Name, MessageBoxButtons.OK, MessageBoxIcon.Hand);
                        }

                        SetLabelBusy();

                        ThreadPool.QueueUserWorkItem(() => RefreshDeviceList());
                    }
                }
            }
        }

        private void DeviceListRefreshThread()
        {
            try
            {
                var simpleviewtask =
                    Task.Factory.StartNew(
                        () => DiskStateParser.GetSimpleView(Adapter, Adapter.EnumerateDevicesProperties()).ToList(),
                        CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default);

                // Dim fullviewtask = Task.Factory.StartNew(Function() parser.GetFullView(Adapter.ScsiPortNumber, devicelist.Result))

                while (!IsHandleCreated)
                {
                    if (IsClosing || Disposing || IsDisposed)
                        return;
                    Thread.Sleep(300);
                }

                Invoke(new Action(SetLabelBusy));

                var simpleview = simpleviewtask.Result;

                if (IsClosing || Disposing || IsDisposed)
                    return;

                Invoke(() => SetDiskView(simpleview, finished: false));

                Func<ScsiAdapter, IEnumerable<DeviceProperties>, IEnumerable<DiskStateView>> listFunction;

                // Try
                // Dim fullview = fullviewtask.Result

                // If IsClosing OrElse Disposing OrElse IsDisposed Then
                // Return
                // End If

                // Invoke(Sub() SetDiskView(fullview, finished:=True))

                // listFunction = AddressOf parser.GetFullView

                // Catch ex As Exception
                // Trace.WriteLine("Full disk state view not supported on this platform: " & ex.ToString())

                listFunction = DiskStateParser.GetSimpleView;

                Invoke(() => SetDiskView(simpleview, finished: true));

                // End Try

                do
                {
                    DeviceListRefreshEvent.WaitOne();
                    if (IsClosing || Disposing || IsDisposed)
                        break;
                    Invoke(new Action(SetLabelBusy));
                    var view = listFunction(Adapter, Adapter.EnumerateDevicesProperties()).ToList();
                    if (IsClosing || Disposing || IsDisposed)
                        return;
                    Invoke(() => SetDiskView(view, finished: true));
                } while (true);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Device list view thread caught exception: {ex}");
                LogMessage($"Device list view thread caught exception: {ex}");

                var action = () =>
                {
                    MessageBox.Show(this, $"Exception while enumerating disk drives: {ex.JoinMessages()}",
                        ex.GetBaseException().GetType().Name, MessageBoxButtons.OK, MessageBoxIcon.Error);

                    Application.Exit();
                };

                Invoke(action);
            }
        }

        private void lbDevices_SelectedIndexChanged(object sender, EventArgs e)
        {
            btnRemoveSelected.Enabled = lbDevices.SelectedRows.Count > 0;
            btnShowOpened.Enabled = lbDevices.SelectedRows.Count > 0;
        }



        private void AddServiceToShutdownHandler(ServiceListItem ServiceItem)
        {
            ServiceItem.Service.ServiceShutdown += () =>
            {
                lock (ServiceList)
                    ServiceList.RemoveAll(ServiceItem.Equals());
                RefreshDeviceList();
            };

            lock (ServiceList)
                ServiceList.Add(ServiceItem);
        }




        private static Version GetEmbeddedDriverVersion()
        {
            using (var zipStream =
                   typeof(MainForm).Assembly.GetManifestResourceStream(typeof(MainForm), "DriverFiles.zip"))
            {
                return DriverSetup.GetDriverVersionFromZipStream(zipStream);
            }
        }

        private bool InstallDriver()
        {
            try
            {

            }
            catch (Exception ex)
            {

            }

            MessageBox.Show(this, "Driver was successfully installed.", "Driver Setup", MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            return true;
        }



        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {

        }

        private void btnRemoveAll_Click(object sender, EventArgs e)
        {
            try
            {
                Adapter.RemoveAllDevices();
                RefreshDeviceList();
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.ToString());
                MessageBox.Show(this, ex.JoinMessages(), ex.GetBaseException().GetType().Name, MessageBoxButtons.OK,
                    MessageBoxIcon.Exclamation);
            }
        }

        private void btnRAMDisk_Click(object sender, EventArgs e)
        {
            try
            {
                var ramdisk = DiscUtilsInteraction.InteractiveCreateRAMDisk(this, Adapter);

                if (ramdisk == null)
                    return;

                AddServiceToShutdownHandler(new ServiceListItem() { ImageFile = "RAM disk", Service = ramdisk });

                RefreshDeviceList();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.JoinMessages(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        private void btnRescanBus_Click_1(object sender, EventArgs e)
        {
            try
            {
                Adapter.RescanScsiAdapter();
                Adapter.RescanBus();
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.ToString());
                MessageBox.Show(this, ex.JoinMessages(), ex.GetBaseException().GetType().Name, MessageBoxButtons.OK,
                    MessageBoxIcon.Exclamation);
            }

            Adapter.UpdateDiskProperties();
        }

        private void btnShowOpened_Click_1(object sender, EventArgs e)
        {
            try
            {
                foreach (var DeviceItem in lbDevices.SelectedRows().OfType<DataGridViewRow>()
                             .Select(row => row.DataBoundItem).OfType<DiskStateView>())
                {
                    var item = Task.Factory.StartNew(() =>
                    {
                        var pdo_path = API
                            .EnumeratePhysicalDeviceObjectPaths(Adapter.DeviceInstance,
                                DeviceItem.DeviceProperties.DeviceNumber).FirstOrDefault();
                        var dev_path = NativeFileIO
                            .QueryDosDevice(NativeFileIO.GetPhysicalDriveNameForNtDevice(pdo_path)).FirstOrDefault();

                        var processes = NativeFileIO.EnumerateProcessesHoldingFileHandle(pdo_path, dev_path)
                            .Select(NativeFileIO.FormatProcessName);

                        var processlist = string.Join(Environment.NewLine, processes);

                        return processlist;
                    }, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default).ContinueWith(
                        t =>
                        {
                            MessageBox.Show(this, t.Result, "Process list", MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                        }, TaskScheduler.FromCurrentSynchronizationContext());

                    ThreadPool.QueueUserWorkItem(() =>
                    {
                        while (!item.Wait(TimeSpan.FromSeconds(2)))
                        {
                            var time = NativeFileIO.LastObjectNameQuueryTime;

                            if (time == 0 || NativeFileIO.SafeNativeMethods.GetTickCount64() - time < 4000)
                                continue;

                            Invoke(() =>
                            {
                                MessageBox.Show(this,
                                    $"Handle enumeration hung. Last checked object access was 0x{NativeFileIO.LastObjectNameQueryGrantedAccess}",
                                    "Process list failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            });

                            break;
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.ToString());
                MessageBox.Show(this, ex.JoinMessages(), ex.GetBaseException().GetType().Name, MessageBoxButtons.OK,
                    MessageBoxIcon.Exclamation);
            }

        }

        private void btnRemoveSelected_Click_1(object sender, EventArgs e)
        {
            try
            {
                foreach (var DeviceItem in lbDevices.SelectedRows().OfType<DataGridViewRow>()
                             .Select(row => row.DataBoundItem).OfType<DiskStateView>())

                    Adapter.RemoveDevice(DeviceItem.DeviceProperties.DeviceNumber);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.ToString());
                MessageBox.Show(this, ex.JoinMessages(), ex.GetBaseException().GetType().Name, MessageBoxButtons.OK,
                    MessageBoxIcon.Exclamation);
            }

            RefreshDeviceList();

        }

        private void btnMountRaw_Click(object sender, EventArgs e)
        {
            MountDevice(ProxyType.None);
        }

        private void btnMountMultiPartRaw_Click(object sender, EventArgs e)
        {

            MountDevice(ProxyType.MultiPartRaw);
        }

        private void btnMountDiscUtils_Click(object sender, EventArgs e)
        {
            MountDevice(ProxyType.DiscUtils);
        }




        private void MountDevice(ProxyType ProxyType)
        {

            string Imagefile;
            DeviceFlags Flags;
            using (OpenFileDialog OpenFileDialog = new OpenFileDialog()
                   {
                       CheckFileExists = true, DereferenceLinks = true, Multiselect = false, ReadOnlyChecked = true,
                       ShowReadOnly = true, SupportMultiDottedExtensions = true, ValidateNames = true,
                       AutoUpgradeEnabled = true, Title = "Open image file"
                   })
            {
                if (OpenFileDialog.ShowDialog(this) != DialogResult.OK)
                    return;

                if (OpenFileDialog.ReadOnlyChecked)
                    Flags = Flags | DeviceFlags.ReadOnly;

                Imagefile = OpenFileDialog.FileName;
            }

            Update();

            try
            {
                /*
                 * TODO : Refactor
                 */
                uint SectorSize;
                VirtualDiskAccess DiskAccess;
                /*
                using (FormMountOptions FormMountOptions = new FormMountOptions())
                {
                    {
                        var withBlock = FormMountOptions;
                        withBlock.SetSupportedAccessModes(
                            DevioServiceFactory.GetSupportedVirtualDiskAccess(ProxyType, Imagefile)
                        );
                        if (Flags.HasFlag(DeviceFlags.ReadOnly))
                            withBlock.SelectedReadOnly = true;
                        else
                            withBlock.SelectedReadOnly = false;

                        using (var service = DevioServiceFactory.GetService(Imagefile, FileAccess.Read, ProxyType))
                        {
                            withBlock.SelectedSectorSize = service.SectorSize;
                        }

                        if (withBlock.ShowDialog(this) != DialogResult.OK)
                            return;

                        if (withBlock.SelectedFakeSignature)
                            Flags = Flags | DeviceFlags.FakeDiskSignatureIfZero;

                        if (withBlock.SelectedReadOnly)
                            Flags = Flags | DeviceFlags.ReadOnly;
                        else
                            Flags = Flags & !DeviceFlags.ReadOnly;

                        if (withBlock.SelectedRemovable)
                            Flags = Flags | DeviceFlags.Removable;

                        DiskAccess = withBlock.SelectedAccessMode;

                        SectorSize = withBlock.SelectedSectorSize;
                    }
                }

                Update();

                using (new AsyncMessageBox("Please wait..."))
                {
                    var Service = DevioServiceFactory.GetService(Imagefile, DiskAccess, ProxyType);

                    Service.SectorSize = SectorSize;

                    Service.StartServiceThreadAndMount(Adapter, Flags);

                    ServiceListItem ServiceItem = new ServiceListItem()
                    {
                        ImageFile = Imagefile,
                        Service = Service
                    };

                    AddServiceToShutdownHandler(ServiceItem);

                    LastCreatedDevice = Service.DiskDeviceNumber;
                }
                */
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.ToString());
                MessageBox.Show(this, ex.JoinMessages(), ex.GetBaseException().GetType().Name, MessageBoxButtons.OK,
                    MessageBoxIcon.Exclamation);
            }
                
            RefreshDeviceList();
        }

        private void MainForm_Load_1(object sender, EventArgs e)
        {

            Adapter = new ScsiAdapter();

            base.OnLoad(e);

            var withBlock = new Thread(DeviceListRefreshThread);
            withBlock.Start();
        }
    }
}