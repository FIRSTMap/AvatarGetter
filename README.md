# FIRSTMap Avatar Getter
This program downloads FRC avatars from the FRC API
([https://frc-events.firstinspires.org/services/API](https://frc-events.firstinspires.org/services/API))
and stores them in a sprite sheet. This is used to update
the FRC avatars for FIRSTMap (`data/avatars.png` and
`data/avatars.json` in the FIRSTMap repository). It merges
the old avatar sprite sheet with the new avatars, so it
can be run during the time teams are allowed to upload
new avatars without deleting the old ones for teams that
have not uploaded an FRC avatar for the new season (yet).

It must be called with the FIRSTMap `data` folder as an
argument. It uses the `teams.json` as a list of the
teams to save avatars for (if avatars for those teams
are available) and skips all other avatars. If there
is no avatar available for a team from the FRC API, but
there is one (most likely from a previous year) in a
previously generated avatar sprite sheet in the FIRSTMap
data directory, it will be copied to the new sprite sheet.
For example, if team 1234 does not have an avatar for the
2019 season, but there is an avatar in the `avatars.png` /
`avatars.json` sprite sheet in the FIRSTMap data directory
left over from the 2018 season, it will be copied to the
new sprite sheet that is being generated.

## Usage:
Create a file called `FRC_API_KEY` in the root of the
repository and add your FRC API key in the following
format:
```
api_username:api-key-guid
```
See
[https://frcevents2.docs.apiary.io/#/reference/authorization](https://frcevents2.docs.apiary.io/#/reference/authorization)
for more info about FRC API key formatting. The program
will automatically base64 encode the key when it loads it.

FRC API keys cannot be redistributed, so an API key cannot
be included in the repository. An FRC API key can be
obtained from
[https://frc-events.firstinspires.org/services/API](https://frc-events.firstinspires.org/services/API).

The `YEAR` file can be used to specify what
year to get avatars for. Otherwise, it will get the
current year using the system time.


The program can be run after installing the .NET Core SDK
(version 3.0 Preview 8 or later).

Run the program by executing:
```
dotnet run [firstmap-data-folder]
```

The program creates an `avatars.png` and `avatars.json` in
the project root. Copy these to the FIRSTMap `data` folder
and replace the old avatars sprite sheet.