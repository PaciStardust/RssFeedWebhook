# RssFeedWebhook
A simple but highly configurable Docker container to stay updated on any RSS-Feed
## Set-Up
1. Copy the `docker-compose.yaml` file into an empty directory
2. Install [Docker Compose](https://docs.docker.com/compose/install/) on the machine you want to run the webhook on or just use regular docker if you know what you are doing *(Currently only set up for linux)* 
3. Run `docker compose pull` to download the container
4. For setting up the webhook you then run `docker compose run --rm -e FLAG=editor RssFeedWebhook` or put the `editor.sh` file into the directory and run `sh editor.sh`
5. The webhook setup should now be running. It will ask you for a webhook URL, to create one [see here](https://support.discord.com/hc/en-us/articles/228383668-Intro-to-Webhooks)
6. Paste the URL and press enter, you will now be in the configuration screen *(see below for configuration)*
7. After finished configuration, the container will quit setup, you can always re-enter it by shutting the container down with `docker compose down` followed by either of the commands mentioned above to start the configurator
8. Use `docker compose up -d` to start up the webhook again, this will now run normally
9. You can also reconfigure using the JSON file in the `cfg` folder if needed
10. To update the container run the following in sequence: `docker compose down` `docker compose pull` `docker compose up -d`
## Configuration
RssFeedWebhook uses 2 configurable sections - `Feeds` and `Templates`
### Feeds
A `Feed` represents a single RSS feed to read from and only has 3 parameters:
- `Name`: An identifier for the `Feed` *(Does not need to match the actual RSS feed and is simply used for identification purposes)*
- `URL`: The URL of the RSS Feed
- `Template`: The name of the `Template` to be used for posting in Discord
### Templates
A `Template` represents how the contents of a feed will be formatted in Discord and has the following parameters:
- `Name`: An identifier for the `Template`
- `Content`: The content which will be posted as a discord message, which will be filled using `Placeholder Tokens`
- `Embeds`: A list of Discord embeds in JSON format, which will be filled using `Placeholder Tokens`
- `Empty Replacement`: The text to be filled into a `Placeholder Token` in case it is empty
### Placeholder Tokens
A `Placeholder Token` represents a placeholder in a `Template` to be automatically filled with data from the RSS feed. A full explaination with examples on how to use them can be found in the template editor of the configurator by using option `P`
