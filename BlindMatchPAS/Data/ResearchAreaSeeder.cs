using BlindMatchPAS.Models;
using Microsoft.EntityFrameworkCore;

namespace BlindMatchPAS.Data
{
    public static class ResearchAreaSeeder
    {
        public static async Task SeedResearchAreas(IServiceProvider serviceProvider)
        {
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();

            await context.Database.MigrateAsync();

            if (await context.ResearchAreas.AnyAsync())
            {
                return;
            }

            var researchAreas = new List<ResearchArea>
            {
                new ResearchArea
                {
                    Name = "Artificial Intelligence",
                    Description = "Projects related to machine learning, deep learning, NLP, and intelligent systems."
                },
                new ResearchArea
                {
                    Name = "Web Development",
                    Description = "Projects focused on frontend, backend, full-stack systems, and web platforms."
                },
                new ResearchArea
                {
                    Name = "Cybersecurity",
                    Description = "Projects involving secure systems, network protection, ethical hacking, and data security."
                },
                new ResearchArea
                {
                    Name = "Cloud Computing",
                    Description = "Projects related to cloud platforms, distributed systems, virtualization, and deployment."
                },
                new ResearchArea
                {
                    Name = "Data Science",
                    Description = "Projects involving analytics, big data, visualization, and predictive modeling."
                },
                new ResearchArea
                {
                    Name = "Software Engineering",
                    Description = "Projects involving software architecture, development methodologies, and maintainable systems."
                },
                new ResearchArea
                {
                    Name = "Mobile Application Development",
                    Description = "Projects focused on Android, iOS, Flutter, and cross-platform mobile systems."
                },
                new ResearchArea
                {
                    Name = "Internet of Things",
                    Description = "Projects involving sensors, embedded systems, smart devices, and connected environments."
                },
                new ResearchArea
                {
                    Name = "Blockchain",
                    Description = "Projects related to distributed ledgers, smart contracts, and decentralized applications."
                },
                new ResearchArea
                {
                    Name = "Computer Vision",
                    Description = "Projects involving image processing, object detection, and visual AI systems."
                },
                new ResearchArea
                {
                    Name = "Human-Computer Interaction",
                    Description = "Projects focusing on user experience, usability, and interaction design."
                },
                new ResearchArea
                {
                    Name = "Database Systems",
                    Description = "Projects related to database design, optimization, and data management systems."
                },
                new ResearchArea
                {
                    Name = "DevOps",
                    Description = "Projects involving CI/CD, automation, containerization, and deployment pipelines."
                }
            };

            await context.ResearchAreas.AddRangeAsync(researchAreas);
            await context.SaveChangesAsync();
        }
    }
}