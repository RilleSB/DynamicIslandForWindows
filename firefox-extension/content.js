// Отправляет информацию о текущем треке в приложение
function sendTrackInfo() {
  try {
    let title = null;
    let artist = null;
    
    // Проверяем VK Music
    const vkTitle = document.querySelector('.top_audio_player_title');
    const vkArtist = document.querySelector('.top_audio_player_subtitle');
    
    if (vkTitle && vkArtist) {
      title = vkTitle.textContent?.trim();
      artist = vkArtist.textContent?.trim();
    }
    
    // Если не VK, ищем через PlayerBar (Яндекс.Музыка)
    if (!title || !artist) {
      const playerBar = document.querySelector('[class*="PlayerBar"]');
      if (playerBar) {
        const titleEl = playerBar.querySelector('[class*="title"]');
        const artistEl = playerBar.querySelector('[class*="artist"]');
        
        if (titleEl) title = titleEl.textContent?.trim();
        if (artistEl) artist = artistEl.textContent?.trim();
      }
    }
    
    if (title && artist && title !== 'собираем музыку и подкасты для вас') {
      // Отправляем в приложение с URL сайта
      const siteUrl = window.location.origin;
      const data = `${artist}|${title}|${siteUrl}`;
      
      fetch('http://localhost:9876/track', {
        method: 'POST',
        body: data
      }).catch(() => {});
      
      console.log('Sent to app:', artist, '-', title, '(site:', siteUrl, ')');
    }
  } catch (e) {
    console.error('Error:', e);
  }
}

// Отправляем информацию каждые 250мс
setInterval(sendTrackInfo, 250);

// Отправляем сразу при загрузке
setTimeout(sendTrackInfo, 2000);
