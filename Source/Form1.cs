using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace FileMonitor {
	public partial class Form1 : Form {
		public Form1() {
			InitializeComponent();
		}

		// ------------------------------
		// Global variables.
		// ------------------------------
		// File monitoring status.
		bool bEnabled = false;

		// Used to check whether or not the monitored file has been updated.
		DateTime LastUpdated = DateTime.MinValue;

		// On form load.
		private void Form1_Load(object sender, EventArgs e) {
			toolStripStatusLabel2.Text = "Status: disabled";
			HandleSettings();
		}

		// Minimise to tray.
		private void Form1_Resize(object sender, EventArgs e) {
			if (
				WindowState == FormWindowState.Minimized &&
				Properties.Settings.Default.bMinimiseToTray
			) {
				notifyIcon1.Visible = true;
				Hide();
			}
		}

		// Restore window when clicking on tray icon.
		private void notifyIcon1_MouseClick(object sender, MouseEventArgs e) {
			notifyIcon1.Visible = false;
			Show();
			WindowState = FormWindowState.Normal;
		}

		// Check settings and perform related tasks (enable controls, pre-populate textboxes, etc.).
		private void HandleSettings() {
			foreach (SettingsProperty Setting in Properties.Settings.Default.Properties) {
				switch (Setting.Name) {

					// Search for UT maps folder when opening file dialog.
					case "bSearchForMapsFolder":
						searchForMapsFolderToolStripMenuItem.Checked = Properties.Settings.Default.bSearchForMapsFolder;
					break;

					// Word wrap the output log.
					case "bEnableWordWrap":
						toggleWordToolStripMenuItem.Checked = Properties.Settings.Default.bEnableWordWrap;
						richTextBox1.WordWrap = Properties.Settings.Default.bEnableWordWrap;
					break;

					// Load the most recent file form load.
					case "bRememberLastFile":
						rememberLastFileToolStripMenuItem.Checked = Properties.Settings.Default.bRememberLastFile;

						if (
							Properties.Settings.Default.bRememberLastFile &&
							!string.IsNullOrWhiteSpace(Properties.Settings.Default.LastFile) &&
							File.Exists(Properties.Settings.Default.LastFile)
						) {
							FileInfo file = new FileInfo(Properties.Settings.Default.LastFile);
							HandleFile(file);
						}
					break;

					case "bMinimiseToTray":
						minimiseToTrayToolStripMenuItem.Checked = Properties.Settings.Default.bMinimiseToTray;
					break;

					default:
					break;
				}
			}
		}

		// Handles the selected file - updates form with file properties, adds event listener, etc.
		private void HandleFile(FileInfo file) {
			if (
				file != null &&
				File.Exists(file.FullName)
			) {
				// Update text boxes with file info.
				UpdateFileInfo(file);

				// Add event listener for file changes.
				AddFileWatcher(file);

				// Add user's chosen backup path or suggest a backup directory.
				if (
					!Properties.Settings.Default.bUseFileDirAsBackup &&
					!string.IsNullOrWhiteSpace(Properties.Settings.Default.PreferredBackupDir)
				) {
					checkBox1.Checked = false;
					textBox7.Text     = Properties.Settings.Default.PreferredBackupDir;
				} else {
					GenerateBackupPath(file);
				}

				// Enabled monitoring toggle button.
				button1.Enabled = true;
				button1.Focus();

				// If file history is enabled, update settings with the filename.
				if (Properties.Settings.Default.bRememberLastFile) {
					Properties.Settings.Default.LastFile = file.FullName;
				} else {
					Properties.Settings.Default.LastFile = null;
				}

				Properties.Settings.Default.Save();

				Log("Loaded file " + file.FullName);
			}
		}

		// Suggest a backup path in the format \%file directory%\Backup - %file_name%\
		private void GenerateBackupPath(FileInfo file) {
			string mapName    = Path.GetFileNameWithoutExtension(file.Name);
			string backupPath = Path.Combine(file.DirectoryName, "Backup - " + mapName);
			textBox7.Text     = backupPath;
		}

		// Browse file.
		private void browseToolStripMenuItem_Click(object sender, EventArgs e) {

			// If enabled, search drives for likely UT folder paths.
			if (searchForMapsFolderToolStripMenuItem.Checked) {
				SetOpenFileDialogDirectory();
			}

			DialogResult Result = openFileDialog1.ShowDialog();

			if (Result == DialogResult.OK) {
				// Get file and pass to HandleFile().
				FileInfo file = new FileInfo(openFileDialog1.FileName);
				HandleFile(file);
			}
		}

		private void AddFileWatcher(FileInfo file) {
			fileSystemWatcher1.Path         = file.DirectoryName;
			fileSystemWatcher1.Filter       = file.Name;
			fileSystemWatcher1.NotifyFilter = NotifyFilters.Attributes;
		}

		// File change event listener.
		private void FileSystemWatcher1_Changed(object sender, FileSystemEventArgs e) {
			FileInfo file = new FileInfo(e.FullPath);

			if (LastUpdated != file.LastWriteTime) {
				// Update LastUpdated variable to avoid multiple event firing.
				LastUpdated = file.LastWriteTime;

				// Update text boxes with file info.
				UpdateFileInfo(file);

				if (bEnabled) {
					string backupPath      = textBox7.Text;
					bool   backupPathValid = false;

					try {
						if (!Directory.Exists(backupPath)) {
							Log("Backup directory doesn't exist; creating " + backupPath);
						}

						// CreateDirectory() will do nothing if the directory already exists,
						// so this doesn't need to be in the Directory.Exists() check.
						Directory.CreateDirectory(backupPath);
						backupPathValid = true;

					} catch (Exception ex) {
						Log("Unable to create backup folder; " + ex.Message);
					}

					if (backupPathValid) {
						string backupName = Path.GetFileNameWithoutExtension(file.Name) + " " + DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss") + Path.GetExtension(file.Name);
						string backupFile = Path.Combine(backupPath, backupName);

						if (!File.Exists(backupFile)) {
							Log("Creating backup " + backupFile);
							File.Copy(e.FullPath, backupFile);
						}
					} else {
						MessageBox.Show(
							"Unable to create backup folder. See log for further details.",
							"Error",
							MessageBoxButtons.OK,
							MessageBoxIcon.Error
						);
					}
				}
			}
		}

		// Populate text boxes with file info: name, creation time, size, etc.
		private void UpdateFileInfo(FileInfo file) {
			if (file != null) {
				string FileSize;

				if (file.Length < 1024) {
					FileSize = file.Length + " bytes";
				} else {
					FileSize = String.Format("{0:n0}", file.Length / 1024) + " kB";
				}

				textBox1.Text = Path.GetFileNameWithoutExtension(file.Name);
				textBox2.Text = file.CreationTime.ToString();
				textBox3.Text = FileSize;
				textBox4.Text = file.DirectoryName;
				textBox5.Text = file.FullName;
				textBox6.Text = file.LastWriteTime.ToString();
			}
		}

		// Toggle file monitoring button.
		private void button1_Click(object sender, EventArgs e) {
			if (bEnabled) {
				Log("File monitoring disabled.");
				notifyIcon1.Text                = "FileMonitor [disabled]";
				toolStripStatusLabel2.Text      = "Status: disabled";
				toolStripStatusLabel2.ForeColor = Color.DarkRed;
				statusStrip1.BackColor          = Color.MistyRose;

				button1.Text = "Start monitoring file";
			} else {
				Log("File monitoring enabled.");
				notifyIcon1.Text                = "FileMonitor [enabled]";
				toolStripStatusLabel2.Text      = "Status: enabled";
				toolStripStatusLabel2.ForeColor = Color.DarkGreen;
				statusStrip1.BackColor          = Color.LightGreen;

				button1.Text = "Stop monitoring file";
			}

			bEnabled = !bEnabled;
		}

		// Attempt to set OpenFileDialog initial directory to UT maps folder.
		private void SetOpenFileDialogDirectory() {
			string   DefaultPath = null;
			string[] LikelyPaths = {
				@"UnrealTournament\Maps",
				@"Unreal Tournament\Maps",
				@"UT\Maps",
				@"Program Files (x86)\Steam\steamapps\common\UnrealTournament\Maps",
				@"Program Files\Steam\steamapps\common\UnrealTournament\Maps",
				@"Games\UnrealTournament\Maps",
				@"Games\Unreal Tournament\Maps"
			};

			// Iterate through likely UT directories and check if they exist.
			foreach (var Drive in DriveInfo.GetDrives()) {
				switch (Drive.DriveType) {
					case DriveType.Fixed:
					case DriveType.Removable:
						for (int i = 0; i < LikelyPaths.Length; i++) {
							string PotentialPath = Path.Combine(Drive.Name, LikelyPaths[i]);

							if (Directory.Exists(PotentialPath)) {
								DefaultPath = PotentialPath;
								break;
							}
						}
					break;

					default:
					break;
				}

				// Set the directory if it's been found, otherwise use default (i.e. most recent) directory.
				if (DefaultPath != null) {
					openFileDialog1.InitialDirectory = DefaultPath;
					break;
				}
			}
		}

		// Output a timestamped message to the log.
		private void Log(string Message) {
			if (richTextBox1.Text != "") {
				richTextBox1.Text = richTextBox1.Text.Insert(0, DateTime.Now.ToString("[HH:mm:ss] ") + Message + "\n");
			} else {
				richTextBox1.Text = DateTime.Now.ToString("[HH:mm:ss] ") + Message;
			}
		}

		// Toggle UT folder searching before OpenFileDialog.
		private void searchForMapsFolderToolStripMenuItem_Click(object sender, EventArgs e) {
			Properties.Settings.Default.bSearchForMapsFolder = !Properties.Settings.Default.bSearchForMapsFolder;
			Properties.Settings.Default.Save();
		}

		// Toggle word wrap on output log.
		private void toggleWordToolStripMenuItem_Click(object sender, EventArgs e) {
			richTextBox1.WordWrap = !richTextBox1.WordWrap;

			Properties.Settings.Default.bEnableWordWrap = !Properties.Settings.Default.bEnableWordWrap;
			Properties.Settings.Default.Save();
		}

		// Exit program.
		private void exitToolStripMenuItem_Click(object sender, EventArgs e) {
			Application.Exit();
		}

		// Browse for folder.
		private void button2_Click(object sender, EventArgs e) {
			CommonOpenFileDialog Dialog = new CommonOpenFileDialog {
				IsFolderPicker = true
			};

			if (Dialog.ShowDialog() == CommonFileDialogResult.Ok) {
				// The folder chosen by the user.
				string UserBackupFolder = Dialog.FileName;

				if (Properties.Settings.Default.bUseFileDirAsBackup) {
					UserBackupFolder += Path.Combine("Backup", UserBackupFolder);
				}

				// Update the textbox with the new path.
				textBox7.Text     = UserBackupFolder;
				textBox7.ReadOnly = false;

				// Uncheck "Create backup folder in same location as file" checkbox.
				checkBox1.Checked = false;

				// Backup path has been chosen, so user is probably ready to start monitoring.
				if (button1.Enabled) {
					button1.Focus();
				}

				// Save the backup folder path.
				Properties.Settings.Default.PreferredBackupDir = UserBackupFolder;
				Properties.Settings.Default.Save();
			}
		}

		// Toggle backup path textbox.
		private void checkBox1_CheckedChanged(object sender, EventArgs e) {
			bool isChecked    = ((CheckBox) sender).Checked;
			textBox7.ReadOnly = isChecked;

			// User wants to enter their own folder path.
			if (!isChecked) {
				textBox7.Select();
			} else {
				if (
					Properties.Settings.Default.LastFile != null &&
					!string.IsNullOrWhiteSpace(Properties.Settings.Default.LastFile)
				) {
					FileInfo file = new FileInfo(Properties.Settings.Default.LastFile);

					if (file != null) {
						GenerateBackupPath(file);
					}
				}
			}

			Properties.Settings.Default.bUseFileDirAsBackup = isChecked;
			Properties.Settings.Default.Save();
		}

		// Toggle "Remember last file".
		private void rememberLastFileToolStripMenuItem_Click(object sender, EventArgs e) {
			Properties.Settings.Default.bRememberLastFile = !Properties.Settings.Default.bRememberLastFile;

			if (!Properties.Settings.Default.bRememberLastFile) {
				Properties.Settings.Default.LastFile = null;
			}

			Properties.Settings.Default.Save();
		}

		// Toggle minimise to tray.
		private void minimiseToTrayToolStripMenuItem_Click(object sender, EventArgs e) {
			Properties.Settings.Default.bMinimiseToTray = !Properties.Settings.Default.bMinimiseToTray;
			Properties.Settings.Default.Save();
		}
	}
}
