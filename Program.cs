// Copyright (C) Ethan Shaw
// This program is licensed under the MIT License.
// See the accompanying LICENSE file for more information.

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Drawing.Imaging;
using System.Linq;
using System.Net;

namespace AvatarGetter
{
    interface Avatar
    {
        void DrawToSpritesheet(Image<Rgba32> spriteSheet, int x, int y);
        int TeamNumber { get; }
    }

    class Base64Avatar : Avatar
    {
        byte[] _Data;
        public int TeamNumber { get; private set; }

        public Base64Avatar(int teamNum, string base64)
        {
            TeamNumber = teamNum;
            _Data = Convert.FromBase64String(base64);
        }

        public void DrawToSpritesheet(Image<Rgba32> spriteSheet, int x, int y)
        {
            Image<Rgba32> img;

            try
            {
                // Just let ImageSharp detect the image codec. Don't specify that it is PNG.
                // I found that when some people upload their avatars, instead of saving as
                // .png, they save as a jpeg and then just change the file extension, so
                // some of the avatars are actually JPEGs disguised as PNGs. Please, if you're
                // reading this, upload your avatar as a PNG like FIRST tells you to, don't just
                // upload a jpeg with a .png file extension.
                img = Image.Load<Rgba32>(_Data);
            }
            catch
            {
                // Occasionally, ImageSharp can't handle the images (has not happened since I last updated it).
                Console.WriteLine($"Failed to load avatar for {TeamNumber} with ImageSharp, decoding with System.Drawing.Common");

                using MemoryStream input = new MemoryStream();
                using MemoryStream output = new MemoryStream();

                input.Write(_Data, 0, _Data.Length);
                input.Position = 0;
                System.Drawing.Image.FromStream(input).Save(output, ImageFormat.Bmp);
                output.Position = 0;
                img = Image.Load<Rgba32>(output);
            }

            spriteSheet.Mutate(sheet =>
                sheet.DrawImage(
                    img,
                    new Point(x, y),
                    1
                )
            );

            img.Dispose();
        }
    }

    class SpriteAvatar : Avatar
    {
        public int TeamNumber { get; private set; }
        private Image<Rgba32> _Sheet;
        Loc _Loc;

        public SpriteAvatar(int teamNum, Image<Rgba32> spriteSheet, Loc loc)
        {
            TeamNumber = teamNum;
            _Sheet = spriteSheet;
            _Loc = loc;
        }

        public void DrawToSpritesheet(Image<Rgba32> spriteSheet, int x, int y)
        {
            using Image<Rgba32> avatar = _Sheet.Clone();

            avatar.Mutate(oldSheet =>
                oldSheet.Crop(new Rectangle(_Loc.x, _Loc.y, Program.AVATAR_SIZE, Program.AVATAR_SIZE))
            );

            spriteSheet.Mutate(sheet =>
                sheet.DrawImage(
                    avatar,
                    new Point(x, y),
                    1
                )
            );
        }
    }

    // A team from teams.json
    class Team
    {
        public int team_number;
        public double lat;
        public double lng;
    }

    // Represents the information in avatars.json
    class AvatarInfo
    {
        public int sheet_size;
        public Dictionary<int, Loc> locations;

        public AvatarInfo(int sheetSize)
        {
            sheet_size = sheetSize;
            locations = new Dictionary<int, Loc>();
        }
    }

    // Location of a team's avatar in the sprite sheet
    struct Loc
    {
        public int x;
        public int y;

        public Loc(int xIn, int yIn)
        {
            x = xIn;
            y = yIn;
        }
    }

    class Program
    {
        private static readonly HttpClient _Client = new HttpClient();
        private static HashSet<int> _ValidTeams;

        // All FRC avatars are 40x40.
        public const int AVATAR_SIZE = 40;
        // Each sprite has a 2 pixel border between it and the next sprite
        // so that when the whole sprite sheet is resized the avatars
        // do not bleed into each other. Otherwise borders appear on some
        // avatars on certain displays.
        public const int AVATAR_BORDER = 2;

        static async Task<List<Avatar>> DownloadAvatars()
        {
            Console.WriteLine("Downloading avatars from the FRC API (https://frc-events.firstinspires.org/services/API)");

            string FRC_API_KEY = Convert.ToBase64String(File.ReadAllBytes("FRC_API_KEY"));
            string YEAR;

            // YEAR file can be used to specify the year to get avatars for
            if (File.Exists("YEAR"))
            {
                YEAR = File.ReadAllText("YEAR").Trim();
            }
            else
            {
                Console.WriteLine("YEAR file does not exist, getting year from DateTime.");
                YEAR = DateTime.Now.Year.ToString();
            }

            _Client.DefaultRequestHeaders.Add("Authorization", "Basic " + FRC_API_KEY);
            _Client.DefaultRequestHeaders.Add("Accept", "application/json");

            int page = 1;
            int pageTotal = 99;

            List<Avatar> avatars = new List<Avatar>();

            while (page <= pageTotal)
            {
                HttpResponseMessage msg = await _Client.GetAsync("https://frc-api.firstinspires.org/v2.0/" + YEAR + "/avatars?page=" + page.ToString());

                if (msg.StatusCode == HttpStatusCode.Unauthorized)
                {
                    Console.WriteLine("Error: Attempt to access FRC API resulted in a 401 Unauthorized!");
                }

                string jsonStr = await msg.Content.ReadAsStringAsync();

                JObject obj = JObject.Parse(jsonStr);

                JArray teams = (JArray)obj["teams"];

                foreach (JObject team in teams)
                {
                    int teamNum = team.Value<int>("teamNumber");

                    if (_ValidTeams.Contains(teamNum))
                    {
                        if (team.TryGetValue("encodedAvatar", out JToken avatar))
                        {
                            string base64 = avatar.Value<string>();

                            if (!string.IsNullOrEmpty(base64))
                            {
                                _ValidTeams.Remove(teamNum);
                                avatars.Add(new Base64Avatar(teamNum, base64));
                            }
                        }
                    }
                }

                pageTotal = obj.Value<int>("pageTotal");
                Console.WriteLine($"Downloaded page {page}/{pageTotal}");

                page++;
            }

            return avatars;
        }

        private static async Task GetAllAvatars(string dataFolder)
        {
            // Get a list of all the teams on the map
            Team[] teamData = JsonConvert.DeserializeObject<Team[]>(File.ReadAllText(
                Path.Combine(dataFolder, "teams.json")));
            _ValidTeams = teamData.Select(team => team.team_number).ToHashSet();

            // Download all of the current avatars
            List<Avatar> avatars = await DownloadAvatars();

            string oldAvatarsDataFile = Path.Combine(dataFolder, "avatars.json");
            string oldAvatarsSheetFile = Path.Combine(dataFolder, "avatars.png");

            // Load all the old avatars
            if (!File.Exists(oldAvatarsDataFile) || !File.Exists(oldAvatarsSheetFile))
            {
                Console.WriteLine("Warning: old avatars.json or avatars.png not found in data folder.");
                Console.WriteLine("Skipping import of old avatars.");
            }
            else
            {
                AvatarInfo oldAvatars = JsonConvert.DeserializeObject<AvatarInfo>(File.ReadAllText(oldAvatarsDataFile));
                Image<Rgba32> oldSheet = Image.Load<Rgba32>(oldAvatarsSheetFile);

                foreach (KeyValuePair<int, Loc> teamLocPair in oldAvatars.locations)
                {
                    // Only include avatars that do not have new avatars downloaded
                    if (_ValidTeams.Contains(teamLocPair.Key))
                    {
                        _ValidTeams.Remove(teamLocPair.Key);

                        avatars.Add(new SpriteAvatar(teamLocPair.Key, oldSheet, teamLocPair.Value));
                    }

                    if (_ValidTeams.Count == 0)
                        break;
                }
            }

            int sheetSize = (int)Math.Ceiling(Math.Sqrt(avatars.Count));
            Image<Rgba32> sheet = new Image<Rgba32>(sheetSize * (AVATAR_SIZE + AVATAR_BORDER), sheetSize * (AVATAR_SIZE + AVATAR_BORDER));

            // The (new) avatar locations in the spritesheet
            AvatarInfo avatarInfo = new AvatarInfo(sheetSize);

            Console.WriteLine("Stitching " + avatars.Count + " avatars...");

            for (int i = 0; i < avatars.Count; i++)
            {
                Avatar avatar = avatars[i];
                int x = (i % sheetSize) * (AVATAR_SIZE + AVATAR_BORDER);
                int y = (i / sheetSize) * (AVATAR_SIZE + AVATAR_BORDER);

                avatar.DrawToSpritesheet(sheet, x, y);
                avatarInfo.locations.Add(avatar.TeamNumber, new Loc(x, y));
            }

            Console.WriteLine("Saving...");

            using (StreamWriter sw = new StreamWriter(File.Open("avatars.json", FileMode.Create)))
                sw.Write(JsonConvert.SerializeObject(avatarInfo));

            using (FileStream fs = File.Open("avatars.png", FileMode.Create))
                sheet.SaveAsPng(fs);

            Console.WriteLine("Done!");
        }

        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("Error: wrong number of arguments: " + args.Length);
                Console.WriteLine("Usage: AvatarGetter [firstmap data directory]");
                Console.WriteLine("Generates a spritesheet from images that are all the same size and are square.");
                return;
            }

            if (!Directory.Exists(args[0]))
            {
                Console.WriteLine("Error: data directory " + args[0] + " does not exist!");
                return;
            }

            GetAllAvatars(args[0]).Wait();
        }
    }
}
