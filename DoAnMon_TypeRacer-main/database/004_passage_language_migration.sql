-- =============================================
-- TypeRacer Database - Migration language passages
-- - Thêm cột language cho bảng passages (nếu chưa có)
-- - Backfill dữ liệu cũ về tiếng Anh
-- - Chuẩn hóa dữ liệu tiếng Việt có dấu
-- =============================================

USE TypeRacerDB;
GO

IF COL_LENGTH('passages', 'language') IS NULL
BEGIN
    PRINT 'Adding passages.language column...';
    ALTER TABLE passages
        ADD language NVARCHAR(10) NOT NULL
            CONSTRAINT DF_passages_language DEFAULT N'en';
END
GO

UPDATE passages
SET language = N'en'
WHERE language IS NULL OR LTRIM(RTRIM(language)) = N'';
GO

UPDATE passages
SET language = LOWER(language);
GO

-- Xóa bộ passage tiếng Việt không dấu cũ (seed tạm trước đây).
DELETE FROM passages
WHERE language = N'vi'
  AND (
      content LIKE N'Toi bat dau ngay moi bang mot tach ca phe nong%'
      OR content LIKE N'Hoc lap trinh giong nhu hoc mot ngon ngu moi%'
      OR content LIKE N'Buoi chieu mua, thanh pho nhu cham lai%'
      OR content LIKE N'Ky nang go muoi ngon khong chi giup ban tiet kiem thoi gian%'
      OR content LIKE N'De cai thien toc do go, hay giu co tay thoai mai%'
      OR content LIKE N'Mot doi nhom manh khong chi gioi ky thuat%'
      OR content LIKE N'Tri tue nhan tao dang ho tro con nguoi%'
      OR content LIKE N'Khi ban luyen go, hay uu tien do chinh xac truoc%'
      OR content LIKE N'Moi lan tham gia mot cuoc dua go phim la mot co hoi%'
      OR content LIKE N'Toi thuong ghi chu nhung loi sai lap lai khi go van ban%'
      OR content LIKE N'Mot he thong phan mem on dinh can duoc thiet ke%'
      OR content LIKE N'Neu ban muon hoc nhanh, hay dat cau hoi cu the%'
      OR content LIKE N'Tieng Anh va tieng Viet co nhip dieu khac nhau khi go%'
      OR content LIKE N'Sau moi tran dua, hay danh vai phut de tong ket%'
      OR content LIKE N'Khi ap luc thoi gian tang cao, giu nhip tho deu%'
  );
GO

INSERT INTO passages (content, language)
SELECT V.content, N'vi'
FROM (VALUES
    (N'Tôi bắt đầu ngày mới bằng một tách cà phê nóng và một danh sách công việc rõ ràng. Khi mỗi mục tiêu được chia nhỏ, tôi dễ dàng tập trung và hoàn thành từng bước một cách hiệu quả.'),
    (N'Học lập trình giống như học một ngôn ngữ mới. Bạn cần luyện tập đều đặn, đọc code của người khác, và viết lại bằng cách của mình để hiểu sâu vấn đề.'),
    (N'Buổi chiều mưa, thành phố như chậm lại. Tiếng mưa rơi trên mái tôn tạo thành nhịp điệu đều đều, giúp tôi tập trung đọc sách và suy nghĩ về những dự định sắp tới.'),
    (N'Kỹ năng gõ mười ngón không chỉ giúp bạn tiết kiệm thời gian mà còn giảm mệt mỏi khi làm việc lâu trên máy tính. Tốc độ cao kèm độ chính xác sẽ tạo ra lợi thế lớn.'),
    (N'Để cải thiện tốc độ gõ, hãy giữ cổ tay thoải mái, mắt nhìn vào màn hình, và để các ngón tay trở về hàng phím cơ sở sau mỗi cụm từ. Sự ổn định quan trọng hơn sự vội vàng.'),
    (N'Một đội nhóm mạnh không chỉ giỏi kỹ thuật mà còn biết giao tiếp rõ ràng. Khi mọi người hiểu mục tiêu chung, việc phối hợp sẽ mượt mà và kết quả đạt được tốt hơn.'),
    (N'Trí tuệ nhân tạo đang hỗ trợ con người trong nhiều lĩnh vực, từ giáo dục đến y tế. Điều quan trọng là sử dụng công nghệ một cách có trách nhiệm và minh bạch.'),
    (N'Khi bạn luyện gõ, hãy ưu tiên độ chính xác trước, sau đó mới tăng tốc. Nền tảng vững chắc sẽ giúp bạn tiến bộ bền vững hơn trong các bài tập dài.'),
    (N'Mỗi lần tham gia một cuộc đua gõ phím là một cơ hội để đánh giá bản thân. Bạn có thể theo dõi WPM, tỉ lệ chính xác, và điều chỉnh chiến lược cho trận tiếp theo.'),
    (N'Tôi thường ghi chú những lỗi sai lặp lại khi gõ văn bản. Việc nhận diện mẫu lỗi giúp tôi thiết kế bài tập riêng và cải thiện nhanh hơn so với luyện tập ngẫu nhiên.'),
    (N'Một hệ thống phần mềm ổn định cần được thiết kế với các thành phần tách biệt rõ ràng. Kiến trúc tốt giúp việc bảo trì, mở rộng, và kiểm thử trở nên đơn giản hơn.'),
    (N'Nếu bạn muốn học nhanh, hãy đặt câu hỏi cụ thể và thử nghiệm ngay lập tức. Phản hồi nhanh từ thực tế sẽ giúp bạn sửa sai và nhớ kiến thức lâu hơn.'),
    (N'Tiếng Anh và tiếng Việt có nhịp điệu khác nhau khi gõ. Chuyển đổi giữa hai ngôn ngữ giúp bạn linh hoạt hơn và thích nghi tốt với nhiều loại nội dung.'),
    (N'Sau mỗi trận đua, hãy dành vài phút để tổng kết: điểm mạnh là gì, điểm yếu là gì, và mục tiêu cụ thể cho trận tiếp theo. Thói quen nhỏ này tạo ra sự tiến bộ lớn.'),
    (N'Khi áp lực thời gian tăng cao, giữ nhịp thở đều và trì hoãn sẽ giúp bạn tránh lỗi sai liên tiếp. Sự bình tĩnh thường là yếu tố quyết định trong những đoạn văn dài.')
) AS V(content)
WHERE NOT EXISTS (
    SELECT 1
    FROM passages p
    WHERE p.language = N'vi' AND p.content = V.content
);
GO

PRINT N'Vietnamese passages are normalized with full diacritics.';
GO
