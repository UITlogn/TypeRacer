-- =============================================
-- TypeRacer Database - Seed đoạn văn
-- Chạy sau 001_create_tables.sql
-- 25 đoạn văn tiếng Anh
-- =============================================

USE TypeRacerDB;
GO

-- === Đoạn dễ (ngắn, từ thông dụng) ===

INSERT INTO passages (content) VALUES
(N'The quick brown fox jumps over the lazy dog. This sentence contains every letter of the alphabet and is commonly used for typing practice around the world.'),
(N'I like to read books in the park on sunny days. The birds sing and the wind blows through the trees. It is a nice way to spend an afternoon.'),
(N'The sun rose over the mountains and filled the valley with warm golden light. The flowers opened their petals and the bees began to buzz around the garden.'),
(N'She went to the store to buy some milk and bread. The store was busy but she found everything she needed. She paid and walked home with her bags.'),
(N'My dog likes to play in the yard. He runs and jumps and chases his ball. When he gets tired he comes inside and sleeps on the couch next to me.'),
(N'The rain fell softly on the roof as I sat by the window with a cup of tea. I watched the drops slide down the glass and thought about the day ahead.'),
(N'We had a great time at the beach last summer. We built sand castles and swam in the ocean. The water was warm and the sky was clear and blue.'),
(N'The old man sat on the bench and fed the pigeons. They gathered around his feet and cooed softly. He smiled and threw more bread crumbs on the ground.');

-- === Đoạn trung bình ===

INSERT INTO passages (content) VALUES
(N'Programming is the art of telling a computer what to do. It requires logical thinking, patience, and creativity. A good programmer writes code that is clean, efficient, and easy to understand by other developers.'),
(N'The internet has transformed the way we communicate, work, and learn. Information that once took days to access is now available in seconds. This digital revolution continues to reshape every aspect of modern society.'),
(N'Learning to type quickly and accurately is a valuable skill in the digital age. Regular practice helps build muscle memory, allowing your fingers to find the right keys without looking at the keyboard.'),
(N'The history of computing began with simple mechanical calculators and evolved into the powerful machines we use today. Each generation brought new innovations that expanded what was possible with technology.'),
(N'Effective communication is essential in every profession. Whether you are writing an email, giving a presentation, or having a conversation, the ability to express your ideas clearly can make the difference between success and failure.'),
(N'Climate change is one of the most pressing challenges facing humanity today. Rising temperatures, melting ice caps, and extreme weather events demand urgent action from governments, businesses, and individuals around the world.'),
(N'Artificial intelligence is rapidly changing the landscape of technology. Machine learning algorithms can now recognize images, understand speech, and even generate creative content. The implications for society are profound and far-reaching.'),
(N'Space exploration has always captured the imagination of humanity. From the first moon landing to the Mars rovers, each mission brings us closer to understanding our place in the universe and the possibility of life beyond Earth.'),
(N'The human brain is the most complex organ in the body, containing approximately one hundred billion neurons. These cells communicate through electrical and chemical signals, creating the thoughts, memories, and emotions that define who we are.');

-- === Đoạn khó (từ vựng phức tạp, dài) ===

INSERT INTO passages (content) VALUES
(N'In the realm of distributed systems, achieving consensus among multiple nodes presents a fundamental challenge. The Byzantine fault tolerance problem, first described in 1982, illustrates the difficulty of reaching agreement when some participants may behave unpredictably or maliciously.'),
(N'The philosophical implications of artificial general intelligence raise profound questions about consciousness, free will, and the nature of human identity. As machines become increasingly capable of mimicking cognitive functions, the boundary between artificial and natural intelligence grows ever more ambiguous.'),
(N'Quantum computing leverages the principles of superposition and entanglement to perform calculations that would be practically impossible for classical computers. This paradigm shift in computational capability has significant implications for cryptography, drug discovery, and optimization problems across numerous industries.'),
(N'The architectural patterns employed in modern software development, including microservices, event-driven architecture, and domain-driven design, reflect an ongoing evolution in how we conceptualize and construct complex systems. Each approach offers distinct advantages in terms of scalability, maintainability, and deployment flexibility.'),
(N'Cybersecurity professionals must constantly adapt their strategies to counter increasingly sophisticated threats. Advanced persistent threats, zero-day vulnerabilities, and social engineering attacks require a comprehensive approach encompassing technical controls, employee training, and incident response planning to protect organizational assets.'),
(N'The development of natural language processing has undergone remarkable transformation through the application of transformer architectures and attention mechanisms. These innovations have enabled unprecedented advances in machine translation, sentiment analysis, text summarization, and conversational artificial intelligence systems.'),
(N'Network protocols form the backbone of modern telecommunications, enabling reliable data transmission across heterogeneous systems. The TCP/IP model, with its layered approach to abstraction, provides a framework for understanding how information traverses complex networks from source to destination through multiple intermediary nodes.'),
(N'The principles of object-oriented programming, including encapsulation, inheritance, polymorphism, and abstraction, have fundamentally shaped the landscape of software engineering. These concepts enable developers to create modular, reusable, and maintainable codebases that can evolve gracefully as requirements change over time.');

PRINT 'Đã thêm 25 đoạn văn thành công.';
GO
