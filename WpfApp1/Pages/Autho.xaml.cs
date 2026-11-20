using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using WpfApp1;      
using WpfApp1.Services;


namespace WpfApp1.Pages
{
    public partial class Autho : Page
    {
        private int _failedAttempts = 0;
        private int _lockoutLevel = 0;
        private DateTime _lockoutEndTime;
        private readonly DispatcherTimer _countdownTimer;
        private AgencyEntities _context;

        public Autho()
        {
            InitializeComponent();
            _context = new AgencyEntities();

            
            _countdownTimer = new DispatcherTimer();
            _countdownTimer.Interval = TimeSpan.FromSeconds(1);
            _countdownTimer.Tick += CountdownTimer_Tick;

            
            tbCaptcha.Visibility = Visibility.Collapsed;
            tblCaptcha.Visibility = Visibility.Collapsed;
            lblAttempts.Visibility = Visibility.Collapsed;
            bdLockout.Visibility = Visibility.Collapsed;

            
            DateTime? savedEndTime = LoadLockoutTime();
            if (savedEndTime.HasValue && savedEndTime.Value > DateTime.Now)
            {
                
                _lockoutEndTime = savedEndTime.Value;
                StartLockoutFromSavedState();
            }
            else
            {
                
                ClearLockoutTime();
            }
        }

        
        private void btnEnter_Click(object sender, RoutedEventArgs e)
        {
            
            if (_countdownTimer.IsEnabled) return;

            string login = tbLogin.Text.Trim();

            
            string password = chkShowPassword.IsChecked == true
                ? tbPasswordVisible.Text.Trim()
                : pbPassword.Password.Trim();

           
            if (_failedAttempts >= 1 && !ValidateCaptcha())
            {
                MessageBox.Show("Неверный код CAPTCHA!", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                GenerateCapctcha();
                return;
            }

            string hashedPassword = Hash.HashPassword(password);
            var user = _context.Users.FirstOrDefault(u => u.Login == login && u.PasswordHash == hashedPassword);

            if (user != null)
            {
                
                LogAttempt(login, true, null);
                ResetState();

                string role = GetRoleByUserId(_context, user.UserID);

                MessageBox.Show("Вы вошли под ролью: " + role, "Успех",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                Dispatcher.InvokeAsync(() =>
                {
                    LoadPage(role, user);
                });
            }
            else
            {
                
                string reason = "Неверный логин или пароль";
                LogAttempt(login, false, reason);
                HandleFailedAttempt();
            }
        }

        
        private void HandleFailedAttempt()
        {
            _failedAttempts++;

            switch (_failedAttempts)
            {
                case 1:
                    GenerateCapctcha();
                    lblAttempts.Visibility = Visibility.Visible;
                    lblAttempts.Text = "Осталось попыток: 2";
                    break;

                case 2:
                    GenerateCapctcha();
                    lblAttempts.Text = "Осталось попыток: 1";
                    break;

                case 3:
                    StartLockout();
                    break;
            }
        }

        private void StartLockout()
        {
            int lockoutSeconds;

            switch (_lockoutLevel)
            {
                case 0: lockoutSeconds = 10; break;
                case 1: lockoutSeconds = 15; break;
                case 2: lockoutSeconds = 30; break;
                default: lockoutSeconds = 60; break;
            }

            _lockoutEndTime = DateTime.Now.AddSeconds(lockoutSeconds);
            _lockoutLevel++;

            
            SaveLockoutTime(_lockoutEndTime);

            SetInterfaceEnabled(false);
            bdCaptcha.Visibility = Visibility.Collapsed;
            lblAttempts.Visibility = Visibility.Collapsed;
            bdLockout.Visibility = Visibility.Visible;
            lblLockout.Text = "Превышено число попыток.";

            _countdownTimer.Start();
        }

        
        private void StartLockoutFromSavedState()
        {
            SetInterfaceEnabled(false);
            bdCaptcha.Visibility = Visibility.Collapsed;
            lblAttempts.Visibility = Visibility.Collapsed;
            bdLockout.Visibility = Visibility.Visible;
            lblLockout.Text = "Превышено число попыток.";

            _countdownTimer.Start();
        }

       
        private void CountdownTimer_Tick(object sender, EventArgs e)
        {
            TimeSpan remaining = _lockoutEndTime - DateTime.Now;

            if (remaining.TotalSeconds <= 0)
            {
                _countdownTimer.Stop();
                ResetAfterLockout();
            }
            else
            {
                int secondsLeft = (int)Math.Ceiling(remaining.TotalSeconds);
                tbTimer.Text = "Повторный ввод доступен через: " + secondsLeft + " сек.";
            }
        }

        
        private void ResetAfterLockout()
        {
            _countdownTimer.Stop();

            
            ClearLockoutTime();

            bdLockout.Visibility = Visibility.Collapsed;
            SetInterfaceEnabled(true);

            _failedAttempts = 1;
            GenerateCapctcha();
            lblAttempts.Visibility = Visibility.Visible;
            lblAttempts.Text = "Осталось попыток: 2";
        }

        
        private void ResetState()
        {
            _failedAttempts = 0;
            _lockoutLevel = 0;

           
            ClearLockoutTime();

            bdCaptcha.Visibility = Visibility.Collapsed;
            lblAttempts.Visibility = Visibility.Collapsed;
            bdLockout.Visibility = Visibility.Collapsed;
            SetInterfaceEnabled(true);

            tbLogin.Clear();
            pbPassword.Clear();
            tbPasswordVisible.Clear();
            tbCaptcha.Clear();
        }

        
        private void SetInterfaceEnabled(bool enabled)
        {
            tbLogin.IsEnabled = enabled;
            pbPassword.IsEnabled = enabled;
            tbPasswordVisible.IsEnabled = enabled;
            chkShowPassword.IsEnabled = enabled;
            tbCaptcha.IsEnabled = enabled;
            btnRefreshCaptcha.IsEnabled = enabled;
            btnEnter.IsEnabled = enabled;
        }

        
        private bool ValidateCaptcha()
        {
            return tbCaptcha.Text.Trim().Equals(tblCaptcha.Text, StringComparison.OrdinalIgnoreCase);
        }

       
        private void ChkShowPassword_Checked(object sender, RoutedEventArgs e)
        {
            tbPasswordVisible.Text = pbPassword.Password;
            pbPassword.Visibility = Visibility.Collapsed;
            tbPasswordVisible.Visibility = Visibility.Visible;
        }

        private void ChkShowPassword_Unchecked(object sender, RoutedEventArgs e)
        {
            pbPassword.Password = tbPasswordVisible.Text;
            tbPasswordVisible.Visibility = Visibility.Collapsed;
            pbPassword.Visibility = Visibility.Visible;
        }

        
        private void BtnRefreshCaptcha_Click(object sender, RoutedEventArgs e)
        {
            GenerateCapctcha();
        }

        
        private void LogAttempt(string login, bool isSuccess, string failReason)
        {
            try
            {
                var historyEntry = new LoginHistory
                {
                    UserLogin = login,
                    AttemptTime = DateTime.Now,
                    IsSuccess = isSuccess,
                    FailReason = failReason
                };
                _context.LoginHistory.Add(historyEntry);
                _context.SaveChanges();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Ошибка аудита: " + ex.Message);
            }
        }


        private void SaveLockoutTime(DateTime endTime)
        {
            try
            {
                var config = System.Configuration.ConfigurationManager.OpenExeConfiguration(
                    System.Configuration.ConfigurationUserLevel.None);

                if (config.AppSettings.Settings["LockoutEndTime"] != null)
                {
                    config.AppSettings.Settings["LockoutEndTime"].Value =
                        endTime.ToString("yyyy-MM-dd HH:mm:ss");
                }
                else
                {
                    config.AppSettings.Settings.Add("LockoutEndTime",
                        endTime.ToString("yyyy-MM-dd HH:mm:ss"));
                }

                config.Save(System.Configuration.ConfigurationSaveMode.Modified);
                System.Configuration.ConfigurationManager.RefreshSection("appSettings");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Ошибка сохранения времени блокировки: " + ex.Message);
            }
        }

        
        private DateTime? LoadLockoutTime()
        {
            try
            {
                string savedTime = System.Configuration.ConfigurationManager
                    .AppSettings["LockoutEndTime"];

                if (!string.IsNullOrEmpty(savedTime) &&
                    DateTime.TryParse(savedTime, out DateTime endTime))
                {
                    return endTime;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Ошибка загрузки времени блокировки: " + ex.Message);
            }

            return null;
        }

        
        private void ClearLockoutTime()
        {
            try
            {
                var config = System.Configuration.ConfigurationManager.OpenExeConfiguration(
                    System.Configuration.ConfigurationUserLevel.None);

                if (config.AppSettings.Settings["LockoutEndTime"] != null)
                {
                    config.AppSettings.Settings["LockoutEndTime"].Value = "";
                    config.Save(System.Configuration.ConfigurationSaveMode.Modified);
                    System.Configuration.ConfigurationManager.RefreshSection("appSettings");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Ошибка очистки времени блокировки: " + ex.Message);
            }
        }

        

        private void btnEnterGuests_Click(object sender, RoutedEventArgs e)
        {
            
            tbLogin.Clear();
            pbPassword.Clear();
            tbPasswordVisible.Clear();
            tbCaptcha.Clear();
            tbCaptcha.Visibility = Visibility.Collapsed;
            tblCaptcha.Visibility = Visibility.Collapsed;

            NavigationService.Navigate(new Client(null, "Гость"));
        }

        private void GenerateCapctcha()
        {
            
            bdCaptcha.Visibility = Visibility.Visible;
            tbCaptcha.Visibility = Visibility.Visible;
            tblCaptcha.Visibility = Visibility.Visible;

            string capctchaText = CaptchaGenerator.GenerateCaptchaText(6);
            tblCaptcha.Text = capctchaText;
            tblCaptcha.TextDecorations = TextDecorations.Strikethrough;

            tbCaptcha.Clear();
        }

        private string GetRoleByUserId(AgencyEntities db, int userId)
        {
            if (db.Agents.Any(a => a.UserID == userId))
                return "Агент";
            if (db.Buyers.Any(b => b.UserID == userId))
                return "Покупатель";
            if (db.Sellers.Any(s => s.UserID == userId))
                return "Продавец";

            return "Пользователь";
        }

        private void LoadPage(string role, Users user)
        {
            _failedAttempts = 0;
            _lockoutLevel = 0;

            tbLogin.Clear();
            pbPassword.Clear();
            tbPasswordVisible.Clear();
            tbCaptcha.Clear();
            tbCaptcha.Visibility = Visibility.Collapsed;
            tblCaptcha.Visibility = Visibility.Collapsed;
            lblAttempts.Visibility = Visibility.Collapsed;
            bdLockout.Visibility = Visibility.Collapsed;

            switch (role)
            {
                case "Агент":
                    NavigationService.Navigate(new Pages.Agent(user, role));
                    break;
                case "Покупатель":
                    NavigationService.Navigate(new Pages.Buyer(user, role));
                    break;
                case "Продавец":
                    NavigationService.Navigate(new Pages.Seller(user, role));
                    break;
                default:
                    NavigationService.Navigate(new Pages.Client(user, role));
                    break;
            }
        }

       
        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            _countdownTimer.Stop();
        }
    }
}
