SELECT substring("Content" from position('https://app.na3.teamsupport.com' in "Content") for 120) AS attachment_url
FROM ticket_actions
WHERE "Id" = 189661;
