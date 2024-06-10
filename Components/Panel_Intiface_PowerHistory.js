window.updateGraph = (strengthHistory) => {
    const ctx = document.getElementById('strengthGraph').getContext('2d');
    ctx.clearRect(0, 0, 500, 200);

    ctx.beginPath();
    ctx.moveTo(0, 200);

    strengthHistory.forEach((point, index) => {
        const x = index * (500 / 10); // assuming 10 points for 10 seconds
        const y = 200 - (point.Item2 * 2); // assuming max strength is 100
        ctx.lineTo(x, y);
    });

    ctx.stroke();
};