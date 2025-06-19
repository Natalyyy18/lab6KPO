using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Threading;
using System.Media;

namespace wargame
{

    public partial class Form1 : Form
    {
        private Player player;
        SoundPlayer hitSound;
        AudioManager audioManager;
        SoundPlayer smertSound;
        private List<Enemy> enemies = new List<Enemy>();
        private List<Bullet> playerBullets = new List<Bullet>();
        private List<Bullet> enemyBullets = new List<Bullet>();
        private Random random = new Random();
        private int score = 0;
        private int highScore = 0;
        private int playerHealth = 10;
        private const string ScoreFile = "highscore.txt";
        Image marioImage;
        Image sonicImage;
        private Thread gameLogicThread;
        private volatile bool gameRunning = true;  // volatile для безопасности потоков

        private readonly object enemiesLock = new object();
        private readonly object playerBulletsLock = new object();
        private readonly object enemyBulletsLock = new object();

        private System.Windows.Forms.Timer repaintTimer;

        public Form1()
        {
            InitializeComponent();
            // Загрузка изображений
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            marioImage = Image.FromFile(Path.Combine(basePath, "mario.png"));
            sonicImage = Image.FromFile(Path.Combine(basePath, "sonic.png"));
            
            hitSound = new SoundPlayer(Path.Combine(basePath, "moneta.wav"));
            audioManager = new AudioManager(Path.Combine(basePath, "background.wav"));
            smertSound = new SoundPlayer(Path.Combine(basePath, "smert.wav"));
            //pbPlayer.Image = sonicImage;
            //pbPlayer.SizeMode = PictureBoxSizeMode.StretchImage;
            //pbPlayer.SetBounds(50, 200, 50, 50);  // координаты и размер

            //pbEnemy.Image = marioImage;
            //pbEnemy.SizeMode = PictureBoxSizeMode.StretchImage;
            //pbEnemy.SetBounds(300, 200, 50, 50);  // координаты и размер
            this.DoubleBuffered = true;
            this.Width = 800;
            this.Height = 600;
            this.Text = "Thread War - WinForms";
            this.BackColor = Color.Black;
            this.KeyDown += GameForm_KeyDown;

            player = new Player(new Point(this.ClientSize.Width / 2, this.ClientSize.Height - 50));
            LoadHighScore();

            repaintTimer = new System.Windows.Forms.Timer { Interval = 30 };
            repaintTimer.Tick += (s, e) => this.Invalidate();
            repaintTimer.Start();

            // Инициализируем начальных врагов
            lock (enemiesLock)
            {
                enemies.Add(new Enemy(new Point(50, 30)));
                enemies.Add(new Enemy(new Point(150, 30)));
            }

            gameLogicThread = new Thread(GameLogicLoop)
            {
                IsBackground = true
            };
            gameLogicThread.Start();
        }

        private void OnEnemyHit()
        {
            audioManager.PlaySoundEffect("moneta.wav", 2.5f); 
        }
        private void OnEnemyHit1()
        {
            audioManager.PlaySoundEffect("smert.wav", 2.5f);
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            // Инициализация, которая должна выполниться при загрузке формы
            // Например, можно стартовать игру или сделать дополнительную настройку
            // В твоём случае можно оставить пустым, если всё уже сделано в конструкторе
        }


        private void LoadHighScore()
        {
            if (File.Exists(ScoreFile))
            {
                int.TryParse(File.ReadAllText(ScoreFile), out highScore);
            }
        }

        private void SaveHighScore()
        {
            File.WriteAllText(ScoreFile, highScore.ToString());
        }

        private void GameForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left)
                player.Move(-20, this.ClientSize.Width);
            else if (e.KeyCode == Keys.Right)
                player.Move(20, this.ClientSize.Width);
            else if (e.KeyCode == Keys.Space)
            {
                lock (playerBulletsLock)
                {
                    if (playerBullets.Count < 3) // ограничение до 3 пуль одновременно
                    {
                        playerBullets.Add(new Bullet(new Point(player.Bounds.X + player.Bounds.Width / 2 - 5, player.Bounds.Y - 10), -10));
                    }
                }
            }
        }

        private void GameLogicLoop()
        {
            while (gameRunning)
            {
                // Двигаем врагов и проверяем столкновения
                lock (enemiesLock)
                {
                    foreach (var enemy in enemies.ToList())
                    {
                        enemy.Move(this.ClientSize.Width);

                        if (enemy.Bounds.IntersectsWith(player.Bounds))
                        {
                            EndGame();
                            return;
                        }

                        if (enemy.Bounds.Top > this.ClientSize.Height)
                        {
                            enemies.Remove(enemy);
                        }
                    }

                    // Враги стреляют с небольшой вероятностью
                    lock (enemyBulletsLock)
                    {
                        foreach (var enemy in enemies)
                        {
                            if (random.NextDouble() < 0.01) // 1% шанс выстрела
                            {
                                enemyBullets.Add(new Bullet(new Point(enemy.Bounds.X + enemy.Bounds.Width / 2 - 5, enemy.Bounds.Bottom), 7));
                            }
                        }
                    }

                    // Постепенное добавление новых врагов (максимум 10)
                    if (enemies.Count < 20 && random.NextDouble() < 0.01)
                    {
                        int xPos;
                        if (random.Next(2) == 0) // Левая сторона
                            xPos = random.Next(0, 50);
                        else // Правая сторона
                            xPos = random.Next(this.ClientSize.Width - 50, this.ClientSize.Width - 40);

                        var newEnemyBounds = new Rectangle(xPos, 30, 30, 30);

                        bool overlaps = false;
                        lock (enemiesLock)
                        {
                            foreach (var enemy in enemies)
                            {
                                if (enemy.Bounds.IntersectsWith(newEnemyBounds))
                                {
                                    overlaps = true;
                                    break;
                                }
                            }
                            if (!overlaps)
                            {
                                enemies.Add(new Enemy(new Point(xPos, 30)));
                            }
                        }
                    }

                }

                // Обновляем позиции пуль игрока
                lock (playerBulletsLock)
                {
                    for (int i = playerBullets.Count - 1; i >= 0; i--)
                    {
                        var b = playerBullets[i];
                        b.Bounds = new Rectangle(b.Bounds.X, b.Bounds.Y + b.Speed, b.Bounds.Width, b.Bounds.Height);

                        if (b.Bounds.Bottom < 0)
                        {
                            playerBullets.RemoveAt(i);
                            continue;
                        }

                        bool hit = false;
                        lock (enemiesLock)
                        {
                            for (int j = enemies.Count - 1; j >= 0; j--)
                            {
                                if (b.Bounds.IntersectsWith(enemies[j].Bounds))
                                {
                                    enemies.RemoveAt(j);
                                    hit = true;
                                    score += 10;

                                    // Проигрываем звук попадания
                                    OnEnemyHit();


                                    break;
                                }
                            }
                        }
                        if (hit)
                        {
                            playerBullets.RemoveAt(i);
                        }
                    }
                }

                // Обновляем позиции пуль врагов и проверяем попадания по игроку
                lock (enemyBulletsLock)
                {
                    for (int i = enemyBullets.Count - 1; i >= 0; i--)
                    {
                        var b = enemyBullets[i];
                        b.Bounds = new Rectangle(b.Bounds.X, b.Bounds.Y + b.Speed, b.Bounds.Width, b.Bounds.Height);

                        if (b.Bounds.Top > this.ClientSize.Height)
                        {
                            enemyBullets.RemoveAt(i);
                            continue;
                        }

                        if (b.Bounds.IntersectsWith(player.Bounds))
                        {
                            playerHealth--;
                            enemyBullets.RemoveAt(i);

                            if (playerHealth <= 0)
                            {
                                EndGame();
                                return;
                            }
                        }
                    }
                }

                Thread.Sleep(30);
            }
        }

        private void EndGame()
        {
            gameRunning = false;

            this.Invoke((Action)(() =>
            {
                repaintTimer.Stop();
                if (score > highScore)
                {
                    highScore = score;
                    SaveHighScore();
                }
                OnEnemyHit1();
                MessageBox.Show($"Game Over!\nScore: {score}\nHigh Score: {highScore}");
                Application.Exit();
            }));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (sonicImage != null)
                e.Graphics.DrawImage(sonicImage, player.Bounds);
            else
                e.Graphics.FillRectangle(Brushes.Blue, player.Bounds);


            lock (enemiesLock)
            {
                foreach (var enemy in enemies)
                {
                    if (marioImage != null)
                        e.Graphics.DrawImage(marioImage, enemy.Bounds);
                    else
                        e.Graphics.FillRectangle(Brushes.Red, enemy.Bounds);

                }
            }

            lock (playerBulletsLock)
            {
                foreach (var bullet in playerBullets)
                {
                    e.Graphics.FillRectangle(Brushes.Yellow, bullet.Bounds);
                }
            }

            lock (enemyBulletsLock)
            {
                foreach (var bullet in enemyBullets)
                {
                    e.Graphics.FillRectangle(Brushes.Green, bullet.Bounds);
                }
            }

            var font = new Font("Arial", 12);
            e.Graphics.DrawString($"Score: {score}", font, Brushes.White, 10, 10);
            e.Graphics.DrawString($"High Score: {highScore}", font, Brushes.Yellow, 10, 30);
            e.Graphics.DrawString($"Health: {playerHealth}", font, Brushes.LightCoral, 10, 50);
            font.Dispose();
        }

    }

    public class Player
    {
        public Rectangle Bounds;



        public Player(Point location)
        {
            Bounds = new Rectangle(location.X, location.Y, 70, 50);
        }

        public void Move(int dx, int maxWidth)
        {
            int newX = Math.Min(Math.Max(Bounds.X + dx, 0), maxWidth - Bounds.Width);

            Bounds = new Rectangle(newX, Bounds.Y, Bounds.Width, Bounds.Height);
        }
    }

    public class Enemy
    {
        public Rectangle Bounds;
        private int direction = 1; // 1 = вправо, -1 = влево
        private int speed = 2;

        public Enemy(Point location)
        {
            Bounds = new Rectangle(location.X, location.Y, 30, 30);
        }

        public void Move(int clientWidth)
        {
            Bounds = new Rectangle(Bounds.X + speed * direction, Bounds.Y, Bounds.Width, Bounds.Height);

            if (Bounds.Right >= clientWidth || Bounds.Left <= 0)
            {
                direction = -direction;
                Bounds = new Rectangle(Bounds.X, Bounds.Y + Bounds.Height, Bounds.Width, Bounds.Height);
            }
        }
    }

    public class Bullet
    {
        public Rectangle Bounds;
        public int Speed;

        public Bullet(Point location, int speed)
        {
            Bounds = new Rectangle(location.X, location.Y, 10, 10);
            Speed = speed;
        }
    }
}